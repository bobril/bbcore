using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lib.BuildCache;
using Lib.Composition;
using Lib.DiskCache;
using Lib.Test;
using Lib.ToolsDir;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;
using Lib.Watcher;

namespace Bbcore.Lib;

public interface IBbcoreLibrary
{
    public Task<int> Run(string[] args, IConsoleLogger logger);

    public bool RunBuild(IFsAbstraction files,
        string typeScriptVersion,
        string directory,
        out string? javaScript,
        out string? parsedMessages);

    public bool RunBuild(
        IFsAbstraction files,
        string typeScriptVersion,
        string directory,
        out string? javaScript,
        out string? parsedMessages,
        out string? sourceMap);
}

public partial class BbcoreLibrary : IBbcoreLibrary
{
    static readonly ILogger Logger = new DummyLogger();
    const string FileWithResultOfBuilding = "a.js";

    public async Task<int> Run(string[] args, IConsoleLogger logger)
    {
        var composition = new Composition(
            inDocker: Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != null,
            logger,
            new NativeFsAbstraction());
        composition.ParseCommandLine(args);
        await composition.RunCommand();
        return Environment.ExitCode;
    }

    public bool RunBuild(IFsAbstraction files,
        string typeScriptVersion,
        string directory,
        out string? javaScript,
        out string? parsedMessages)
    {
        javaScript = null;
        parsedMessages = null;

        var context = CreateBundlingContext(files, directory, typeScriptVersion);

        var errors = 0;
        var warnings = 0;

        Transpile(context, ref errors, ref warnings);

        if (errors == 0) Bundle(context);

        if (errors <= 0 && !context.BuildResult.HasError)
        {
            javaScript = context.MainBuildResult.FilesContent.GetOrAddValueRef(FileWithResultOfBuilding) as string;
            return true;
        }

        parsedMessages = ParseMessages(context.Messages);
        return false;
    }

    public bool RunBuild(IFsAbstraction files, string typeScriptVersion, string directory, out string? javaScript,
        out string? parsedMessages, out string? sourceMap)
    {
        javaScript = null!;
        parsedMessages = null!;
        sourceMap = null!;

        var context = CreateBundlingContext(files, directory, typeScriptVersion);

        var errors = 0;
        var warnings = 0;

        Transpile(context, ref errors, ref warnings);

        if (errors == 0) Bundle(context, withSourceMap: true, directory);

        if (errors <= 0 && !context.BuildResult.HasError)
        {
            javaScript = context.MainBuildResult.FilesContent.GetOrAddValueRef(FileWithResultOfBuilding) as string;
            sourceMap =
                context.MainBuildResult.FilesContent.GetOrAddValueRef(FileWithResultOfBuilding + ".map") as string;
            return true;
        }

        parsedMessages = ParseMessages(context.Messages);
        return false;
    }

    [GeneratedRegex(@".*/(.*?)$")]
    private static partial Regex FileNameRegex();

    static string GetFileName(string path)
    {
        var match = FileNameRegex().Match(path);
        return !match.Success ? "" : match.Groups[1].Value;
    }

    static string ParseMessages(IList<Diagnostic> messages, bool onlySemantic = false)
    {
        var result = new StringBuilder();
        foreach (var message in messages)
        {
            if (onlySemantic && !message.IsSemantic) continue;
            if (message.FileName == null)
            {
                if (message.IsError)
                    result.AppendLine(
                        $"Error: {message.Text} ({message.Code})");
                else
                    result.AppendLine(
                        $"Warning: {message.Text} ({message.Code})");
            }
            else
            {
                if (message.IsError)
                    result.AppendLine(
                        $"Error: {GetFileName(message.FileName)} ({message.StartLine + 1},{message.StartCol + 1}): {message.Text} ({message.Code})");
                else
                    result.AppendLine(
                        $"Warning: {GetFileName(message.FileName)} ({message.StartLine + 1},{message.StartCol + 1}): {message.Text} ({message.Code})");
            }
        }

        return result.ToString();
    }

    static void Bundle(BuildContext context, bool withSourceMap = false, string? directory = null)
    {
        context.Bundler =
            new NjsastBundleBundler(
                context.ToolsDir,
                Logger,
                context.MainBuildResult,
                context.TsProject.ProjectOptions!,
                context.BuildResult);

        context.Bundler.Build(
            compress: true,
            mangle: false,
            beautify: true,
            buildSourceMap: withSourceMap,
            sourceMapSourceRoot: directory);
    }

    static BuildContext CreateBundlingContext(IFsAbstraction files,
        string directory,
        string typeScriptVersion)
    {
        var toolsDir = new ToolsDir(directory, Logger, files);
        toolsDir.SetTypeScriptVersion(typeScriptVersion);
        var diskCache = new DiskCache(files, () => new DummyWatcher());
        var mainFilePath = directory == "." ? "/tmp/index.ts" : PathUtils.Join(directory, "index.ts");
        var mainFile = diskCache.TryGetItem(mainFilePath);
        var mainBuildResult = new MainBuildResult(true, null, null);
        var directoryCache = (directory == "." ? diskCache.TryGetItem("/") as IDirectoryCache : mainFile?.Parent) ??
                             throw new InvalidOperationException("Directory for " + mainFilePath + " not found");
        var tsProject = TSProject.Create(directoryCache, diskCache, Logger, null)!;
        tsProject.IsRootProject = true;
        tsProject.ProjectOptions!.ForbiddenDependencyUpdate = true;
        tsProject.ProjectOptions.UpdateFromProjectJson(null);
        tsProject.ProjectOptions.Debug = false;
        tsProject.ProjectOptions.LibraryMode = true;
        tsProject.ProjectOptions.FinalCompilerOptions =
            tsProject.ProjectOptions.GetDefaultTSCompilerOptions();
        tsProject.ProjectOptions.FinalCompilerOptions!.allowJs = true;
        tsProject.ProjectOptions.FinalCompilerOptions.declaration = true;
        tsProject.ProjectOptions.FinalCompilerOptions.module = ModuleKind.Commonjs;
        tsProject.ProjectOptions.FinalCompilerOptions.target = ScriptTarget.Es2019;
        tsProject.ProjectOptions.FinalCompilerOptions.strict = true;
        tsProject.ProjectOptions.FinalCompilerOptions.outDir = "dist";
        tsProject.ProjectOptions.MainFile = "/tmp/index.ts";
        tsProject.ProjectOptions.Tools = toolsDir;
        tsProject.ProjectOptions.TypeScriptVersion = typeScriptVersion;
        var buildCache = new DummyBuildCache();
        var context = new BuildContext
        {
            ToolsDir = toolsDir,
            FileSystemAbstraction = files,
            DiskCache = diskCache,
            MainBuildResult = mainBuildResult,
            DirectoryCache = directoryCache,
            TsProject = tsProject,
            BuildResult = new BuildResult(mainBuildResult, tsProject.ProjectOptions),
            Messages = new List<Diagnostic>(),
            BuildCache = buildCache,
            TranspilationContext = new BuildCtx(
                new CompilerPool(toolsDir, Logger),
                diskCache,
                verbose: true,
                Logger,
                currentDirectory: tsProject.Owner.FullPath,
                buildCache,
                typeCheckValue: "no")
        };

        return context;
    }

    static void Transpile(
        BuildContext context,
        ref int errors,
        ref int warnings)
    {
        var options = context.TsProject.ProjectOptions!;
        context.TranspilationContext.Build(
            options,
            buildOnlyOnce: true,
            context.BuildResult,
            context.MainBuildResult,
            iterationId: 1);

        context.TranspilationContext.CompilerPool.FreeMemory().GetAwaiter();

        IncludeMessages(
            options,
            context.MainBuildResult,
            context.BuildResult,
            ref errors,
            ref warnings,
            context.Messages);
    }

    static void IncludeMessages(
        ProjectOptions options,
        MainBuildResult mainBuildResult,
        BuildResult buildResult,
        ref int errors,
        ref int warnings,
        IList<Diagnostic> messages)
    {
        var relativeTo = mainBuildResult.CommonSourceDirectory ?? options.Owner.Owner.FullPath;
        foreach (var pathInfoPair in buildResult.Path2FileInfo)
        {
            var diag = pathInfoPair.Value.Diagnostics;
            foreach (var d in diag)
            {
                if (options.IgnoreDiagnostic?.Contains(d.Code) ?? false)
                    continue;
                var isError = d.IsError || options.WarningsAsErrors;
                if (isError)
                    errors++;
                else
                    warnings++;
                messages.Add(new Diagnostic
                {
                    FileName = PathUtils.ForDiagnosticDisplay(pathInfoPair.Key, relativeTo, relativeTo),
                    IsError = isError,
                    Text = d.Text,
                    Code = d.Code,
                    StartLine = d.StartLine,
                    StartCol = d.StartCol,
                    EndLine = d.EndLine,
                    EndCol = d.EndCol,
                });
            }
        }

        if (buildResult.SubBuildResults == null) return;

        foreach (var subBuildResult in buildResult.SubBuildResults)
            IncludeMessages(options, mainBuildResult, subBuildResult.Value, ref errors, ref warnings, messages);
    }
}
