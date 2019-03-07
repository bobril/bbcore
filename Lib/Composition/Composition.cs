﻿using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Watcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using Lib.WebServer;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using Lib.Utils.CommandLineParser.Definitions;
using Lib.Utils.CommandLineParser.Parser;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Lib.Chrome;
using System.Reflection;
using System.Text;
using System.Reactive;
using Lib.BuildCache;
using Lib.Registry;
using Lib.Utils.Notification;
using Lib.Translation;
using Lib.Utils.Logger;

namespace Lib.Composition
{
    public class Composition
    {
        readonly bool _inDocker;
        string _bbdir;
        ToolsDir.IToolsDir _tools;
        DiskCache.DiskCache _dc;
        CompilerPool _compilerPool;
        object _projectsLock = new object();
        List<ProjectOptions> _projects = new List<ProjectOptions>();
        WebServerHost _webServer;
        AutoResetEvent _hasBuildWork = new AutoResetEvent(true);
        ProjectOptions _currentProject;
        CommandLineCommand _command;
        TestServer _testServer;
        ILongPollingServer _testServerLongPollingHandler;
        MainServer _mainServer;
        ILongPollingServer _mainServerLongPollingHandler;
        IChromeProcessFactory _chromeProcessFactory;
        IChromeProcess _chromeProcess;
        IBuildCache _buildCache;
        bool _verbose;
        bool _forbiddenDependencyUpdate;
        NotificationManager _notificationManager;
        readonly IConsoleLogger _logger = new ConsoleLogger();

        public Composition(bool inDocker)
        {
            _inDocker = inDocker;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            InitTools();
        }

        public void ParseCommandLine(string[] args)
        {
            _command = CommandLineParser.Parse
            (
                args,
                new List<CommandLineCommand>()
                {
                    new BuildCommand(),
                    new TranslationCommand(),
                    new TestCommand(),
                    new BlameTestCommand(),
                    new BuildInteractiveCommand(),
                    new BuildInteractiveNoUpdateCommand(),
                    new PackageManagerCommand()
                }
            );
        }

        public void RunCommand()
        {
            if (_command == null)
                return;
            if (_command is CommonParametersBaseCommand commonParams)
            {
                if (commonParams.Verbose.Value)
                    _verbose = true;
                if (commonParams.NoBuildCache.Value)
                    _buildCache = new DummyBuildCache();
                else
                    _buildCache = new PersistentBuildCache(_tools.Path);
            }

            if (_command is BuildInteractiveCommand)
            {
                RunInteractive(_command as CommonInteractiveCommand);
            }
            else if (_command is BuildInteractiveNoUpdateCommand)
            {
                _forbiddenDependencyUpdate = true;
                RunInteractive(_command as CommonInteractiveCommand);
            }
            else if (_command is BuildCommand bCommand)
            {
                RunBuild(bCommand);
            }
            else if (_command is TestCommand testCommand)
            {
                RunTest(testCommand);
            }
            else if (_command is BlameTestCommand blameTestCommand)
            {
                RunBlameTest(blameTestCommand);
            }
            else if (_command is TranslationCommand tCommand)
            {
                RunTranslation(tCommand);
            }
            else if (_command is PackageManagerInstallCommand installCommand)
            {
                RunPackageInstall(installCommand);
            }
            else if (_command is PackageManagerUpgradeCommand upgradeCommand)
            {
                RunPackageUpgrade(upgradeCommand);
            }
            else if (_command is PackageManagerAddCommand addCommand)
            {
                RunPackageAdd(addCommand);
            }
        }

        void RunPackageAdd(PackageManagerAddCommand addCommand)
        {
            InitDiskCache();
            var project = AddProject(PathUtils.Normalize(Environment.CurrentDirectory), false);
            var currentNodePackageManager = new CurrentNodePackageManager(_dc, _logger);
            if (addCommand.PackageName.Value == null)
            {
                _logger.Error("Didn't specified name of package to add");
            }
            else
            {
                currentNodePackageManager.Add(project.Owner.Owner, addCommand.PackageName.Value, addCommand.Dev.Value);
            }
        }

        void RunPackageUpgrade(PackageManagerUpgradeCommand upgradeCommand)
        {
            InitDiskCache();
            var project = AddProject(PathUtils.Normalize(Environment.CurrentDirectory), false);
            var currentNodePackageManager = new CurrentNodePackageManager(_dc, _logger);
            var before = currentNodePackageManager.GetLockedDependencies(project.Owner.Owner).ToArray();
            if (upgradeCommand.PackageName.Value == null)
            {
                currentNodePackageManager.UpgradeAll(project.Owner.Owner);
            }
            else
            {
                currentNodePackageManager.Upgrade(project.Owner.Owner, upgradeCommand.PackageName.Value);
            }

            _dc.CheckForTrueChange();
            var after = currentNodePackageManager.GetLockedDependencies(project.Owner.Owner).ToArray();
            var lines = new List<string>();
            foreach (var line in new DiffChangeLog(_dc).Generate(before, after))
            {
                lines.Add(line);
                _logger.WriteLine(line);
            }

            if (lines.Count > 0)
                try
                {
                    File.WriteAllText(PathUtils.Join(project.Owner.Owner.FullPath, "DEPSCHANGELOG.md"),
                        string.Join('\n', lines) + '\n', Encoding.UTF8);
                }
                catch (Exception e)
                {
                    _logger.Error($"Write DEPSCHANGELOG.md failed with {e}");
                }
        }

        void RunPackageInstall(PackageManagerInstallCommand installCommand)
        {
            InitDiskCache();
            var project = AddProject(PathUtils.Normalize(Environment.CurrentDirectory), false);
            new CurrentNodePackageManager(_dc, _logger).Install(project.Owner.Owner);
        }

        void RunTranslation(TranslationCommand tCommand)
        {
            InitDiskCache();
            var project = AddProject(PathUtils.Normalize(Environment.CurrentDirectory), false);
            TranslationDb trDb;
            var addLanguage = tCommand.AddLang.Value;
            if (addLanguage != null)
            {
                project.InitializeTranslationDb();
                trDb = project.TranslationDb;
                addLanguage = addLanguage.ToLowerInvariant();
                if (trDb.HasLanguage(addLanguage))
                {
                    _logger.WriteLine($"Cannot add language {addLanguage} because it already exists. Doing nothing.");
                }
                else
                {
                    _logger.WriteLine($"Adding language {addLanguage}");
                    trDb.AddLanguage(addLanguage);
                    trDb.SaveLangDb(PathToTranslations(project), addLanguage, false);
                    _logger.WriteLine($"Added language {addLanguage}");
                }

                return;
            }

            var removeLanguage = tCommand.RemoveLang.Value;
            if (removeLanguage != null)
            {
                project.InitializeTranslationDb();
                trDb = project.TranslationDb;
                if (!trDb.HasLanguage(removeLanguage))
                {
                    _logger.Warn($"Cannot remove language {removeLanguage} because it does not exist. Doing nothing.");
                }
                else
                {
                    _logger.WriteLine($"Removing language {removeLanguage}");
                    File.Delete(PathUtils.Join(PathToTranslations(project), $"{removeLanguage}.json"));
                    _logger.WriteLine($"Removed language {removeLanguage}");
                }

                return;
            }

            var export = tCommand.Export.Value;
            var exportAll = tCommand.ExportAll.Value;
            var lang = tCommand.Lang.Value;
            var specificPath = tCommand.SpecificPath.Value;
            if (export != null || exportAll != null)
            {
                project.InitializeTranslationDb(specificPath);
                trDb = project.TranslationDb;

                if (lang != null && !trDb.HasLanguage(lang))
                {
                    _logger.Error(
                        $"You have entered unsupported language '{lang}'. Please enter one of {string.Join(',', trDb.GetLanguages())}");
                    return;
                }

                var destinationFile = export;
                var exportOnlyUntranslated = true;

                if (exportAll != null)
                {
                    destinationFile = exportAll;
                    exportOnlyUntranslated = false;
                }

                if (!trDb.ExportLanguages(destinationFile, exportOnlyUntranslated, lang, specificPath))
                    _logger.Warn("Nothing to export. No export file created.");
                else
                {
                    if (specificPath == null)
                    {
                        _logger.WriteLine(lang != null
                            ? $"Exported {(exportOnlyUntranslated ? "untranslated " : string.Empty)}language '{lang}' to {destinationFile}."
                            : $"Exported {(exportOnlyUntranslated ? "untranslated " : string.Empty)}languages to {destinationFile}.");
                    }
                    else
                        _logger.WriteLine($"Exported file from {specificPath} into file {destinationFile}");
                }

                return;
            }

            var import = tCommand.Import.Value;
            if (import != null)
            {
                project.InitializeTranslationDb(specificPath);
                trDb = project.TranslationDb;
                if (specificPath == null)
                {
                    if (!trDb.ImportTranslatedLanguage(import, specificPath))
                    {
                        _logger.Error("Import failed. See output for more information.");
                        return;
                    }

                    var importedLang = Path.GetFileNameWithoutExtension(PathUtils.Normalize(import));
                    trDb.SaveLangDb(PathToTranslations(project), importedLang, false);

                    _logger.WriteLine($"Translated language from file {import} successfully imported.");
                }
                else
                {
                    if (!trDb.ImportTranslatedLanguage(import, specificPath))
                    {
                        _logger.Error("Import failed. See output for more information.");
                        return;
                    }

                    var language = trDb.GetLanguageFromSpecificFile(specificPath);
                    var dir = Path.GetDirectoryName(specificPath);
                    trDb.SaveLangDb(dir, language, false);

                    _logger.WriteLine(
                        $"Translated language from file {import} successfully imported to file {specificPath}.");
                }

                return;
            }

            var union = tCommand.Union.Value;
            if (union != null && union.All(x => x != null))
            {
                if (union.Length != 3)
                {
                    _logger.Error("Incorrect count of parameters.");
                    return;
                }

                project.InitializeTranslationDb();
                trDb = project.TranslationDb;

                if (trDb.UnionExportedLanguage(union[0], union[1], union[2]))
                    _logger.Success($"Union of {union[0]} with {union[1]} successfully saved to {union[2]}");

                return;
            }

            var subtract = tCommand.Subtract.Value;
            if (subtract != null && subtract.All(x => x != null))
            {
                if (subtract.Length != 3)
                {
                    _logger.Error("Incorrect count of parameters.");
                    return;
                }

                project.InitializeTranslationDb();
                trDb = project.TranslationDb;

                if (trDb.SubtractExportedLanguage(subtract[0], subtract[1], subtract[2]))
                    _logger.Success(
                        $"Subtract of {subtract[0]} with {subtract[1]} successfully saved to {subtract[2]}");

                return;
            }
        }

        void IfEnabledStartVerbosive()
        {
            if (!_verbose)
                return;
            _logger.WriteLine("Verbose output enabled");
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
        }

        void CurrentDomain_FirstChanceException(object sender,
            System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            string s = e.Exception.ToString();
            if (s.Contains("KestrelConnectionReset"))
                return;
            _logger.WriteLine("First chance exception: " + s);
        }

        void RunBuild(BuildCommand bCommand)
        {
            InitDiskCache();
            AddProject(PathUtils.Normalize(Environment.CurrentDirectory), bCommand.Sprite.Value);
            _forbiddenDependencyUpdate = bCommand.NoUpdate.Value;
            DateTime start = DateTime.UtcNow;
            int errors = 0;
            int warnings = 0;
            var messages = new List<CompilationResultMessage>();
            var messagesFromFiles = new HashSet<string>();
            var totalFiles = 0;
            foreach (var proj in _projects)
            {
                try
                {
                    _logger.WriteLine("Build started " + proj.Owner.Owner.FullPath, ConsoleColor.Blue);
                    proj.Owner.LoadProjectJson(_forbiddenDependencyUpdate);
                    if (bCommand.Localize.Value != null)
                        proj.Localize = bCommand.Localize.Value ?? false;
                    proj.Owner.InitializeOnce();
                    proj.OutputSubDir = bCommand.VersionDir.Value;
                    proj.CompressFileNames = !bCommand.Fast.Value;
                    proj.StyleDefNaming =
                        ParseStyleDefNaming(bCommand.Style.Value ?? (bCommand.Fast.Value ? "2" : "0"));
                    proj.BundleCss = !bCommand.Fast.Value;
                    proj.Defines["DEBUG"] = bCommand.Fast.Value;
                    proj.GenerateCode();
                    proj.SpriterInitialization();
                    proj.RefreshMainFile();
                    proj.DetectBobrilJsxDts();
                    proj.RefreshExampleSources();
                    var ctx = new BuildCtx(_compilerPool, _verbose, ShowTsVersion);
                    ctx.TSCompilerOptions = proj.GetDefaultTSCompilerOptions();
                    ctx.Sources = new HashSet<string>();
                    ctx.Sources.Add(proj.MainFile);
                    proj.ExampleSources.ForEach(s => ctx.Sources.Add(s));
                    if (proj.BobrilJsxDts != null)
                        ctx.Sources.Add(proj.BobrilJsxDts);
                    proj.Owner.Build(ctx);
                    var buildResult = ctx.BuildResult;
                    var filesContent = new Dictionary<string, object>();
                    proj.FillOutputByAdditionalResourcesDirectory(filesContent);
                    IncludeMessages(proj, buildResult, ref errors, ref warnings, messages, messagesFromFiles,
                        proj.Owner.Owner.FullPath);
                    if (errors == 0)
                    {
                        if (proj.Localize && bCommand.UpdateTranslations.Value)
                        {
                            proj.TranslationDb.SaveLangDbs(PathToTranslations(proj), true);
                        }
                        else
                        {
                            if (bCommand.Fast.Value)
                            {
                                var fastBundle = new FastBundleBundler(_tools);
                                fastBundle.FilesContent = filesContent;
                                fastBundle.Project = proj;
                                fastBundle.BuildResult = buildResult;
                                fastBundle.Build("bb/base", "bundle.js.map");
                            }
                            else
                            {
                                var bundle = new BundleBundler(_tools);
                                bundle.FilesContent = filesContent;
                                bundle.Project = proj;
                                bundle.BuildResult = buildResult;
                                bundle.Build(bCommand.Compress.Value, bCommand.Mangle.Value, bCommand.Beautify.Value);
                            }

                            SaveFilesContentToDisk(filesContent, bCommand.Dir.Value);
                            totalFiles += filesContent.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Fatal Error: " + ex);
                    errors++;
                }
            }

            var duration = DateTime.UtcNow - start;
            PrintMessages(messages);
            var color = errors != 0 ? ConsoleColor.Red : warnings != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            _logger.WriteLine(
                $"Build done in {duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s with {Plural(errors, "error")} and {Plural(warnings, "warning")} and has {Plural(totalFiles, "file")}",
                color);

            Environment.ExitCode = errors != 0 ? 1 : 0;
        }

        void PrintMessages(IList<CompilationResultMessage> messages)
        {
            foreach (var message in messages)
            {
                if (message.IsError)
                {
                    _logger.Error(
                        $"{message.FileName}({(message.Pos[0])},{(message.Pos[1])}): {message.Text} ({message.Code})");
                }
                else
                {
                    _logger.Warn(
                        $"{message.FileName}({(message.Pos[0])},{(message.Pos[1])}): {message.Text} ({message.Code})");
                }
            }
        }

        string _lastTsVersion = null;

        void ShowTsVersion(string version)
        {
            if (_lastTsVersion != version)
            {
                _logger.WriteLine("Using TypeScript version " + version);
                _lastTsVersion = version;
            }
        }

        static string PathToTranslations(ProjectOptions proj)
        {
            return PathUtils.Join(proj.Owner.Owner.FullPath, "translations");
        }

        void RunTest(TestCommand testCommand)
        {
            InitDiskCache();
            InitTestServer();
            InitMainServer();
            AddProject(PathUtils.Normalize(Environment.CurrentDirectory), testCommand.Sprite.Value);
            int port = 0;
            if (int.TryParse(testCommand.Port.Value, out var portInInt))
            {
                port = portInInt;
            }

            StartWebServer(port, false);
            DateTime start = DateTime.UtcNow;
            int errors = 0;
            int testFailures = 0;
            int warnings = 0;
            var messages = new List<CompilationResultMessage>();
            var messagesFromFiles = new HashSet<string>();
            var totalFiles = 0;
            foreach (var proj in _projects)
            {
                try
                {
                    _logger.WriteLine("Test build started " + proj.Owner.Owner.FullPath, ConsoleColor.Blue);
                    TestResultsHolder testResults = new TestResultsHolder();
                    proj.Owner.LoadProjectJson(true);
                    if (testCommand.Localize.Value != null)
                        proj.Localize = testCommand.Localize.Value ?? false;
                    proj.Owner.InitializeOnce();
                    proj.StyleDefNaming = StyleDefNamingStyle.AddNames;
                    proj.GenerateCode();
                    proj.SpriterInitialization();
                    proj.RefreshMainFile();
                    proj.DetectBobrilJsxDts();
                    proj.RefreshTestSources();
                    if (proj.TestSources != null && proj.TestSources.Count > 0)
                    {
                        var ctx = new BuildCtx(_compilerPool, _verbose, ShowTsVersion);
                        ctx.TSCompilerOptions = proj.GetDefaultTSCompilerOptions();
                        ctx.Sources = new HashSet<string>();
                        ctx.Sources.Add(proj.JasmineDts);
                        proj.TestSources.ForEach(s => ctx.Sources.Add(s));
                        if (proj.BobrilJsxDts != null)
                            ctx.Sources.Add(proj.BobrilJsxDts);
                        proj.Owner.Build(ctx);
                        var testBuildResult = ctx.BuildResult;
                        var fastBundle = new FastBundleBundler(_tools);
                        var filesContent = new Dictionary<string, object>();
                        proj.FillOutputByAdditionalResourcesDirectory(filesContent);
                        fastBundle.FilesContent = filesContent;
                        fastBundle.Project = proj;
                        fastBundle.BuildResult = testBuildResult;
                        fastBundle.Build("bb/base", "testbundle.js.map", true);
                        proj.TestProjFastBundle = fastBundle;
                        proj.FilesContent = filesContent;
                        if (testCommand.Dir.Value != null)
                            SaveFilesContentToDisk(filesContent, testCommand.Dir.Value);
                        IncludeMessages(proj, proj.TestProjFastBundle, ref errors, ref warnings, messages,
                            messagesFromFiles,
                            proj.Owner.Owner.FullPath);
                        PrintMessages(messages);
                        if (errors == 0)
                        {
                            var wait = new Semaphore(0, 1);
                            _testServer.OnTestResults.Subscribe(results =>
                            {
                                testFailures = results.TestsFailed;
                                testResults = results;
                                wait.Release();
                            });
                            var durationb = DateTime.UtcNow - start;

                            _logger.Success("Build successful. Starting Chrome to run tests in " +
                                            durationb.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s");

                            _testServer.StartTest("/test.html",
                                new Dictionary<string, SourceMap> {{"testbundle.js", testBuildResult.SourceMap}}, testCommand.SpecFilter.Value);
                            StartChromeTest();
                            wait.WaitOne();
                            StopChromeTest();
                        }
                    }

                    if (testCommand.Out.Value != null)
                        File.WriteAllText(testCommand.Out.Value,
                            testResults.ToJUnitXml(testCommand.FlatTestSuites.Value), new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    _logger.Error("Fatal Error: " + ex);
                    errors++;
                }
            }

            var duration = DateTime.UtcNow - start;
            var color = (errors + testFailures) != 0 ? ConsoleColor.Red :
                warnings != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            _logger.WriteLine(
                "Test done in " + duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " with " +
                Plural(errors, "error") + " and " + Plural(warnings, "warning") + " and has " +
                Plural(totalFiles, "file") + " and " + Plural(testFailures, "failure"), color);

            Environment.ExitCode = (errors + testFailures) != 0 ? 1 : 0;
        }

        void RunBlameTest(BlameTestCommand testCommand)
        {
            InitDiskCache();
            InitTestServer(false);
            InitMainServer();
            AddProject(PathUtils.Normalize(Environment.CurrentDirectory), testCommand.Sprite.Value);
            int port = 0;
            if (int.TryParse(testCommand.Port.Value, out var portInInt))
            {
                port = portInInt;
            }

            StartWebServer(port, false);

            var rng = new Random();
            foreach (var proj in _projects)
            {
                try
                {
                    _logger.WriteLine("Test build started " + proj.Owner.Owner.FullPath, ConsoleColor.Blue);
                    proj.Owner.LoadProjectJson(true);
                    if (testCommand.Localize.Value != null)
                        proj.Localize = testCommand.Localize.Value ?? false;
                    proj.Owner.InitializeOnce();
                    proj.StyleDefNaming = StyleDefNamingStyle.AddNames;
                    proj.GenerateCode();
                    proj.SpriterInitialization();
                    proj.RefreshMainFile();
                    proj.DetectBobrilJsxDts();
                    proj.RefreshTestSources();
                    if (proj.TestSources == null) proj.TestSources = new List<string>();
                    var dtsFiles = proj.TestSources.Where(s => s.EndsWith(".d.ts", StringComparison.Ordinal)).ToArray();
                    var specFiles = proj.TestSources.Where(s => !s.EndsWith(".d.ts", StringComparison.Ordinal))
                        .ToList();
                    var end = false;
                    var removeChunk = (specFiles.Count + 1) / 2;
                    while (specFiles.Count > 0 && !end)
                    {
                        if (removeChunk >= specFiles.Count)
                        {
                            removeChunk = (specFiles.Count + 1) / 2;
                        }

                        end = true;
                        var orderOfRemoval = Enumerable.Range(0, specFiles.Count).ToArray();
                        for (var i = orderOfRemoval.Length - 1; i > -1; i--)
                        {
                            var j = rng.Next(i);
                            var tmp = orderOfRemoval[i];
                            orderOfRemoval[i] = orderOfRemoval[j];
                            orderOfRemoval[j] = tmp;
                        }

                        for (var ti = 0; ti < orderOfRemoval.Length - removeChunk + 1; ti += removeChunk)
                        {
                            var testResults = new TestResultsHolder();
                            var testFailures = 0;
                            var errors = 0;
                            var warnings = 0;
                            var messages = new List<CompilationResultMessage>();
                            var messagesFromFiles = new HashSet<string>();
                            var ctx = new BuildCtx(_compilerPool, _verbose, ShowTsVersion);
                            ctx.TSCompilerOptions = proj.GetDefaultTSCompilerOptions();
                            ctx.Sources = new HashSet<string>();
                            ctx.Sources.Add(proj.JasmineDts);
                            proj.TestSources = new List<string>();
                            for (var k = ti; k < ti + specFiles.Count - removeChunk + 1; k++)
                            {
                                var item = specFiles[orderOfRemoval[k % orderOfRemoval.Length]];
                                ctx.Sources.Add(item);
                                proj.TestSources.Add(item);
                            }

                            _logger.Info($"{ti}/{proj.TestSources.Count} ChunkSize:{removeChunk}");

                            foreach (var dtsFile in dtsFiles)
                            {
                                ctx.Sources.Add(dtsFile);
                            }

                            if (proj.BobrilJsxDts != null)
                                ctx.Sources.Add(proj.BobrilJsxDts);
                            proj.Owner.Build(ctx);
                            var testBuildResult = ctx.BuildResult;
                            var fastBundle = new FastBundleBundler(_tools);
                            var filesContent = new Dictionary<string, object>();
                            proj.FillOutputByAdditionalResourcesDirectory(filesContent);
                            fastBundle.FilesContent = filesContent;
                            fastBundle.Project = proj;
                            fastBundle.BuildResult = testBuildResult;
                            fastBundle.Build("bb/base", "testbundle.js.map", true);
                            proj.TestProjFastBundle = fastBundle;
                            proj.FilesContent = filesContent;
                            IncludeMessages(proj, proj.TestProjFastBundle, ref errors, ref warnings, messages,
                                messagesFromFiles,
                                proj.Owner.Owner.FullPath);
                            PrintMessages(messages);
                            if (errors == 0)
                            {
                                var wait = new Semaphore(0, 1);
                                using (_testServer.OnTestResults.Subscribe(results =>
                                {
                                    testFailures = results.TestsFailed;
                                    testResults = results;
                                    wait.Release();
                                }))
                                {
                                    var startOfTest = DateTime.UtcNow;
                                    _testServer.StartTest("/test.html",
                                        new Dictionary<string, SourceMap>
                                            {{"testbundle.js", testBuildResult.SourceMap}}, testCommand.SpecFilter.Value);
                                    StartChromeTest();
                                    try
                                    {
                                        if (!wait.WaitOne(120000))
                                        {
                                            _logger.Error($"First run 120s timeout");
                                            continue;
                                        }

                                        if (testFailures != 0)
                                        {
                                            _logger.Error($"{testFailures} failures");
                                            continue;
                                            /*
                                            File.WriteAllText("out.xml",
                                                testResults.ToJUnitXml(false), new UTF8Encoding(false));
                                            SaveFilesContentToDisk(filesContent, "dist");
                                            removeChunk = 1;
                                            break;
                                            //*/
                                        }

                                        var firstRunInMS = (DateTime.UtcNow - startOfTest).TotalMilliseconds;
                                        _testServer.StartTest("/test.html",
                                            new Dictionary<string, SourceMap>
                                                {{"testbundle.js", testBuildResult.SourceMap}}, testCommand.SpecFilter.Value);

                                        var timeout = (int) (firstRunInMS * 2 + 1000);
                                        if (timeout < 10) timeout = 120000;
                                        _logger.Info("Running again with " + timeout + "ms timeout");
                                        if (!wait.WaitOne(timeout))
                                        {
                                            _logger.Error("Timeout");
                                            specFiles = proj.TestSources;
                                            end = false;
                                            break;
                                        }

                                        if (testFailures != 0)
                                        {
                                            _logger.Error($"{testFailures} second run failures");
                                            File.WriteAllText("out.xml",
                                                testResults.ToJUnitXml(false), new UTF8Encoding(false));
                                            removeChunk = 1;
                                            break;
                                        }
                                    }
                                    finally
                                    {
                                        StopChromeTest();
                                    }
                                }
                            }
                        }

                        if (end && removeChunk > 1)
                        {
                            removeChunk = removeChunk / 2;
                            end = false;
                        }
                    }

                    specFiles.ForEach(s => _logger.Info(s));
                    _logger.Warn($"Total left {specFiles.Count}");
                }
                catch (Exception ex)
                {
                    _logger.Error("Fatal Error: " + ex);
                }

                Console.ReadLine();
            }
        }

        StyleDefNamingStyle ParseStyleDefNaming(string value)
        {
            switch (value)
            {
                case "0":
                    return StyleDefNamingStyle.RemoveNames;
                case "1":
                    return StyleDefNamingStyle.PreserveNames;
                case "2":
                    return StyleDefNamingStyle.AddNames;
                default:
                    return StyleDefNamingStyle.PreserveNames;
            }
        }

        void SaveFilesContentToDisk(Dictionary<string, object> filesContent, string dir)
        {
            dir = PathUtils.Normalize(dir);
            var utf8WithoutBom = new UTF8Encoding(false);
            foreach (var nameAndContent in filesContent)
            {
                var content = nameAndContent.Value;
                var fileName = PathUtils.Join(dir, nameAndContent.Key);
                if (content is Lazy<object>)
                {
                    content = ((Lazy<object>) content).Value;
                }

                Directory.CreateDirectory(PathUtils.Parent(fileName) ?? ".");
                if (content is string)
                {
                    File.WriteAllText(fileName, (string) content, utf8WithoutBom);
                }
                else
                {
                    File.WriteAllBytes(fileName, (byte[]) content);
                }
            }
        }

        void RunInteractive(CommonInteractiveCommand command)
        {
            if (command.ProxyBB.Value != null)
            {
                _tools.ProxyWeb(command.ProxyBB.Value);
                _logger.Info("Enabling bb proxy to " + command.ProxyBB.Value);
            }

            if (command.ProxyBBTest.Value != null)
            {
                _tools.ProxyWebt(command.ProxyBBTest.Value);
                _logger.Info("Enabling bb/test proxy to " + command.ProxyBBTest.Value);
            }

            IfEnabledStartVerbosive();
            int port = 8080;
            if (int.TryParse(command.Port.Value, out var portInInt))
            {
                port = portInInt;
            }

            InitDiskCache(true);
            InitTestServer();
            InitMainServer();
            AddProject(PathUtils.Normalize(Environment.CurrentDirectory), command.Sprite.Value);
            StartWebServer(port, command.BindToAny.Value);
            InitInteractiveMode(command.VersionDir.Value, command.Localize.Value);
            WaitForStop();
        }

        public void InitTools()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            if (_inDocker)
            {
                _bbdir = "/bbcache";
            }
            else
            {
                var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
                var runningFrom = PathUtils.Normalize(new FileInfo(location.AbsolutePath).Directory.FullName);
                _bbdir = PathUtils.Join(
                    PathUtils.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), ".bbcore");
                if (runningFrom.StartsWith(_bbdir))
                {
                    _bbdir = PathUtils.Join(_bbdir, "tools");
                }
                else
                {
                    _bbdir = PathUtils.Join(_bbdir, "dev");
                }
            }

            _tools = new ToolsDir.ToolsDir(_bbdir, _logger);
            _compilerPool = new CompilerPool(_tools, _logger);
            _notificationManager = new NotificationManager();
        }

        public void InitDiskCache(bool withWatcher = false)
        {
            _dc = _inDocker || !withWatcher
                ? new DiskCache.DiskCache(new NativeFsAbstraction(), () => new DummyWatcher())
                : new DiskCache.DiskCache(new NativeFsAbstraction(), () => new OsWatcher());
        }

        public ProjectOptions AddProject(string path, bool enableSpritting)
        {
            var projectDir = PathUtils.Normalize(new DirectoryInfo(path).FullName);
            var dirCache = _dc.TryGetItem(projectDir) as IDirectoryCache;
            var proj = TSProject.Get(dirCache, _dc, _logger, null);
            proj.IsRootProject = true;
            if (proj.ProjectOptions.BuildCache != null)
                return proj.ProjectOptions;
            proj.ProjectOptions = new ProjectOptions
            {
                Tools = _tools,
                BuildCache = _buildCache,
                Owner = proj,
                Defines = new Dictionary<string, bool> {{"DEBUG", true}},
                SpriteGeneration = enableSpritting
            };
            lock (_projectsLock)
            {
                _projects.Add(proj.ProjectOptions);
            }

            _currentProject = proj.ProjectOptions;
            if (_mainServer != null)
                _mainServer.Project = _currentProject;
            return proj.ProjectOptions;
        }

        public void StartWebServer(int port, bool bindToAny)
        {
            _webServer = new WebServerHost();
            _webServer.InDocker = _inDocker;
            _webServer.FallbackToRandomPort = true;
            _webServer.Port = port;
            _webServer.Handler = Handler;
            _webServer.BindToAny = bindToAny;
            _webServer.Start();
            if (port != _webServer.Port)
            {
                _logger.Warn($"Listening on RANDOM port http://{(bindToAny ? "*" : "localhost")}:{_webServer.Port}/");
            }
            else
            {
                _logger.Info($"Listening on http://{(bindToAny ? "*" : "localhost")}:{_webServer.Port}/");
            }
        }

        async Task Handler(HttpContext context)
        {
            var path = context.Request.Path;
            if (path == "/")
                path = "/index.html";

            if (path.StartsWithSegments("/bb/test", out var bbtest))
            {
                if (bbtest == "")
                {
                    context.Response.Redirect("/bb/test/", true);
                    return;
                }

                if (bbtest == "/")
                {
                    bbtest = "/index.html";
                }

                var bytes = _tools.WebtGet(bbtest);
                if (bytes != null)
                {
                    context.Response.ContentType = PathUtils.PathToMimeType(bbtest);
                    await context.Response.Body.WriteAsync(bytes);
                    return;
                }
            }

            if (path == "/bb/api/test")
            {
                await _testServerLongPollingHandler.Handle(context);
                return;
            }

            if (path == "/bb/api/main")
            {
                await _mainServerLongPollingHandler.Handle(context);
                return;
            }

            if (path == "/bb/api/projectdirectory")
            {
                await context.Response.WriteAsync(_currentProject.Owner.Owner.FullPath);
                return;
            }

            if (path.StartsWithSegments("/bb/api/liveReload", out var liveIdx))
            {
                while (_currentProject.LiveReloadIdx == int.Parse(liveIdx.Value.Substring(1)))
                {
                    await _currentProject.LiveReloadAwaiter.Task;
                    _currentProject.LiveReloadAwaiter = new TaskCompletionSource<Unit>();
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("Reload");
                return;
            }

            if (path.StartsWithSegments("/bb/base", out var src))
            {
                var srcPath = PathUtils.Join(_currentProject.CommonSourceDirectory, src.Value.Substring(1));
                var srcFileCache = _dc.TryGetItem(srcPath) as IFileCache;
                if (srcFileCache != null)
                {
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(srcFileCache.Utf8Content);
                    return;
                }
            }

            if (path.StartsWithSegments("/bb", out var bbweb))
            {
                if (bbweb == "")
                {
                    context.Response.Redirect("/bb/", true);
                    return;
                }

                if (bbweb == "/")
                {
                    bbweb = "/index.html";
                }

                var bytes = _tools.WebGet(bbweb);
                if (bytes != null)
                {
                    context.Response.ContentType = PathUtils.PathToMimeType(bbweb);
                    await context.Response.Body.WriteAsync(bytes);
                    return;
                }
            }

            var pathWithoutFirstSlash = path.Value.Substring(1);
            var filesContentFromCurrentProjectBuildResult = _currentProject.FilesContent;
            object content;
            if (FindInFilesContent(pathWithoutFirstSlash, filesContentFromCurrentProjectBuildResult, out content))
            {
                context.Response.ContentType = PathUtils.PathToMimeType(pathWithoutFirstSlash);
                if (content is Lazy<object>)
                {
                    content = ((Lazy<object>) content).Value;
                }

                if (content is string)
                {
                    await context.Response.WriteAsync((string) content);
                }
                else
                {
                    await context.Response.Body.WriteAsync((byte[]) content, 0, ((byte[]) content).Length);
                }

                return;
            }

            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Not found " + path);
        }

        static bool FindInFilesContent(string pathWithoutFirstSlash,
            Dictionary<string, object> filesContentFromCurrentProjectBuildResult, out object content)
        {
            content = null;
            if (filesContentFromCurrentProjectBuildResult == null)
                return false;
            if (filesContentFromCurrentProjectBuildResult.TryGetValue(pathWithoutFirstSlash, out content))
                return true;
            // This should be very rare so it could be slow linear search
            foreach (var p in filesContentFromCurrentProjectBuildResult)
            {
                if (p.Key.Equals(pathWithoutFirstSlash, StringComparison.InvariantCultureIgnoreCase))
                {
                    content = p.Value;
                    return true;
                }
            }

            return false;
        }

        public void InitTestServer(bool notify = true)
        {
            _testServer = new TestServer(_verbose);
            _testServerLongPollingHandler = new LongPollingServer(_testServer.NewConnectionHandler);
            if (notify)
            {
                _testServer.OnTestResults.Subscribe((results) =>
                {
                    var color = results.TestsFailed != 0 ? ConsoleColor.Red :
                        results.TestsSkipped != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                    _logger.WriteLine(
                        $"Tests on {results.UserAgent} Failed: {results.TestsFailed} Skipped: {results.TestsSkipped} Total: {results.TotalTests} Duration: {results.Duration * 0.001:F1}s",
                        color);
                    _notificationManager.SendNotification(results.ToNotificationParameters());
                });
            }
        }

        public void InitMainServer()
        {
            _mainServer = new MainServer(() => _testServer.GetState());
            _mainServerLongPollingHandler = new LongPollingServer(_mainServer.NewConnectionHandler);
            _testServer.OnChange.Subscribe((_) => { _mainServer.NotifyTestServerChange(); });
        }

        public void InitInteractiveMode(string versionDir, bool? localizeValue)
        {
            _hasBuildWork.Set();
            _dc.ChangeObservable.Throttle(TimeSpan.FromMilliseconds(200)).Subscribe((_) => _hasBuildWork.Set());
            Task.Run(() =>
            {
                while (_hasBuildWork.WaitOne())
                {
                    if (!_dc.CheckForTrueChange())
                        continue;
                    _dc.ResetChange();
                    _hasBuildWork.Set();
                    var start = DateTime.UtcNow;
                    ProjectOptions[] toBuild;
                    lock (_projectsLock)
                    {
                        toBuild = _projects.ToArray();
                    }

                    if (toBuild.Length == 0)
                    {
                        _logger.Error("Change detected, but no project to build");
                        continue;
                    }

                    _mainServer.NotifyCompilationStarted();
                    int errors = 0;
                    int warnings = 0;
                    var messages = new List<CompilationResultMessage>();
                    var messagesFromFiles = new HashSet<string>();
                    var totalFiles = 0;
                    foreach (var proj in toBuild)
                    {
                        _logger.WriteLine("Build started " + proj.Owner.Owner.FullPath, ConsoleColor.Blue);
                        try
                        {
                            proj.Owner.LoadProjectJson(_forbiddenDependencyUpdate);
                            if (localizeValue != null)
                                proj.Localize = localizeValue ?? false;
                            proj.Owner.InitializeOnce();
                            proj.OutputSubDir = versionDir;
                            proj.Owner.UsedDependencies = new HashSet<string>();
                            proj.GenerateCode();
                            proj.RefreshMainFile();
                            proj.RefreshTestSources();
                            proj.SpriterInitialization();
                            proj.DetectBobrilJsxDts();
                            proj.RefreshExampleSources();
                            proj.UpdateTSConfigJson();
                            var ctx = new BuildCtx(_compilerPool, _verbose, ShowTsVersion);
                            ctx.TSCompilerOptions = proj.GetDefaultTSCompilerOptions();
                            ctx.Sources = new HashSet<string>();
                            ctx.Sources.Add(proj.MainFile);
                            proj.ExampleSources.ForEach(s => ctx.Sources.Add(s));
                            if (proj.BobrilJsxDts != null)
                                ctx.Sources.Add(proj.BobrilJsxDts);
                            proj.Owner.Build(ctx);
                            var buildResult = ctx.BuildResult;
                            var filesContent = new Dictionary<string, object>();
                            proj.FillOutputByAdditionalResourcesDirectory(filesContent);
                            var fastBundle = new FastBundleBundler(_tools);
                            fastBundle.FilesContent = filesContent;
                            fastBundle.Project = proj;
                            fastBundle.BuildResult = buildResult;
                            fastBundle.Build("bb/base", "bundle.js.map");
                            proj.MainProjFastBundle = fastBundle;
                            IncludeMessages(proj, proj.MainProjFastBundle, ref errors, ref warnings, messages,
                                messagesFromFiles, proj.Owner.Owner.FullPath);
                            if (errors == 0 && proj.LiveReloadEnabled)
                            {
                                proj.LiveReloadIdx++;
                                proj.LiveReloadAwaiter.TrySetResult(Unit.Default);
                            }

                            if (proj.TestSources != null && proj.TestSources.Count > 0)
                            {
                                ctx = new BuildCtx(_compilerPool, _verbose, ShowTsVersion);
                                ctx.TSCompilerOptions = proj.GetDefaultTSCompilerOptions();
                                ctx.Sources = new HashSet<string>();
                                ctx.Sources.Add(proj.JasmineDts);
                                proj.TestSources.ForEach(s => ctx.Sources.Add(s));
                                if (proj.BobrilJsxDts != null)
                                    ctx.Sources.Add(proj.BobrilJsxDts);
                                proj.Owner.Build(ctx);
                                var testBuildResult = ctx.BuildResult;
                                fastBundle = new FastBundleBundler(_tools);
                                fastBundle.FilesContent = filesContent;
                                fastBundle.Project = proj;
                                fastBundle.BuildResult = testBuildResult;
                                fastBundle.Build("bb/base", "testbundle.js.map", true);
                                proj.TestProjFastBundle = fastBundle;
                                IncludeMessages(proj, proj.TestProjFastBundle, ref errors, ref warnings, messages,
                                    messagesFromFiles, proj.Owner.Owner.FullPath);
                                if (errors == 0)
                                {
                                    _testServer.StartTest("/test.html",
                                        new Dictionary<string, SourceMap>
                                            {{"testbundle.js", testBuildResult.SourceMap}});
                                    StartChromeTest();
                                }
                            }
                            else
                            {
                                proj.TestProjFastBundle = null;
                            }

                            proj.FilesContent = filesContent;
                            totalFiles += filesContent.Count;
                            var unusedDeps = proj.Owner.Dependencies.ToHashSet();
                            unusedDeps.ExceptWith(proj.Owner.UsedDependencies);
                            AddUnusedDependenciesMessages(proj, unusedDeps, ref errors, ref warnings, messages);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Fatal Error: " + ex);
                            errors++;
                        }
                    }

                    var duration = DateTime.UtcNow - start;
                    _mainServer.NotifyCompilationFinished(errors, warnings, duration.TotalSeconds, messages);
                    _notificationManager.SendNotification(
                        NotificationParameters.CreateBuildParameters(errors, warnings, duration.TotalSeconds));
                    PrintMessages(messages);
                    var color = errors != 0 ? ConsoleColor.Red :
                        warnings != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                    _logger.WriteLine(
                        $"Build done in {(DateTime.UtcNow - start).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s with {Plural(errors, "error")} and {Plural(warnings, "warning")} and has {Plural(totalFiles, "file")}",
                        color);
                    _dc.ResetChange();
                }
            });
        }

        void AddUnusedDependenciesMessages(ProjectOptions options, HashSet<string> unusedDeps, ref int errors,
            ref int warnings, List<CompilationResultMessage> messages)
        {
            foreach (var unusedDep in unusedDeps)
            {
                if (unusedDep.StartsWith("@types/", StringComparison.Ordinal))
                {
                    continue;
                }

                const int unusedDependencyCode = -13;
                if (options.IgnoreDiagnostic?.Contains(unusedDependencyCode) ?? false)
                    continue;
                var isError = options.WarningsAsErrors;
                if (isError)
                    errors++;
                else
                    warnings++;
                messages.Add(new CompilationResultMessage
                {
                    FileName = "package.json",
                    IsError = isError,
                    Text = "Unused dependency " + unusedDep + " in package.json",
                    Code = unusedDependencyCode,
                    Pos = new[]
                    {
                        1,
                        1,
                        1,
                        1
                    }
                });
            }
        }

        string Plural(int number, string word)
        {
            if (number == 0)
                return "no " + word + "s";
            return $"{number} {word}{(number > 1 ? "s" : "")}";
        }

        void IncludeMessages(ProjectOptions options, FastBundleBundler fastBundle, ref int errors, ref int warnings,
            List<CompilationResultMessage> messages, HashSet<string> messagesFromFiles, string rootPath)
        {
            IncludeMessages(options, fastBundle.BuildResult, ref errors, ref warnings, messages, messagesFromFiles,
                rootPath);
        }

        void IncludeMessages(ProjectOptions options, BuildResult buildResult, ref int errors, ref int warnings,
            List<CompilationResultMessage> messages, HashSet<string> messagesFromFiles, string rootPath)
        {
            var usedDependencies = options.Owner.UsedDependencies;
            foreach (var pathInfoPair in buildResult.Path2FileInfo)
            {
                if (messagesFromFiles.Contains(pathInfoPair.Key))
                    continue;
                if (usedDependencies != null && options.Owner == pathInfoPair.Value.MyProject)
                {
                    if (pathInfoPair.Value.ModuleImports != null)
                    {
                        foreach (var moduleImport in pathInfoPair.Value.ModuleImports)
                        {
                            usedDependencies.Add(moduleImport.Name);
                        }
                    }

                    var assets = pathInfoPair.Value.SourceInfo?.assets;
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            if (buildResult.Path2FileInfo.TryGetValue(asset.name, out var info))
                            {
                                var module = info.GetFromModule();
                                if (module != null)
                                    usedDependencies.Add(module);
                            }
                        }
                    }
                }

                messagesFromFiles.Add(pathInfoPair.Key);
                var diag = pathInfoPair.Value.Diagnostic;
                if (diag == null)
                    continue;
                foreach (var d in diag)
                {
                    if (options.IgnoreDiagnostic?.Contains(d.code) ?? false)
                        continue;
                    var isError = d.isError || options.WarningsAsErrors;
                    if (isError)
                        errors++;
                    else
                        warnings++;
                    messages.Add(new CompilationResultMessage
                    {
                        FileName = PathUtils.Subtract(pathInfoPair.Key, rootPath),
                        IsError = isError,
                        Text = d.text,
                        Code = d.code,
                        Pos = new[]
                        {
                            d.startLine + 1,
                            d.startCharacter + 1,
                            d.endLine + 1,
                            d.endCharacter + 1
                        }
                    });
                }
            }
        }

        public void StartChromeTest()
        {
            if (_chromeProcessFactory == null)
            {
                var chromePath = ChromePathFinder.GetChromePath(new NativeFsAbstraction());
                _chromeProcessFactory = new ChromeProcessFactory(_inDocker, chromePath);
            }

            if (_chromeProcess == null)
            {
                try
                {
                    _chromeProcess = _chromeProcessFactory.Create($"http://localhost:{_webServer.Port}/bb/test/");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed To Start Chrome Headless");
                    Console.WriteLine(ex);
                }
            }
        }

        public void StopChromeTest()
        {
            if (_chromeProcess != null)
            {
                _chromeProcess.Dispose();
                _chromeProcess = null;
            }
        }

        public void WaitForStop()
        {
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs args) =>
            {
                ExitWithCleanUp();
                args.Cancel = true;
            };
            while (true)
            {
                var line = Console.ReadLine();
                if (line == "f" || line == "free" || line == "free ram")
                    _compilerPool.FreeMemory().GetAwaiter();
                if (line == "q" || line == "quit")
                    break;
            }

            ExitWithCleanUp();
        }

        public void ExitWithCleanUp()
        {
            _logger.WriteLine("Stopping Chrome");
            StopChromeTest();
            _logger.WriteLine("Exitting");
            Environment.Exit(0);
        }
    }
}