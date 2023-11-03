using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
    
public static partial class BbcoreLibrary
{
    private static readonly ILogger Logger = new DummyLogger();
    private const string FileWithResultOfBuilding = "a.js";
    
    public static void RunBuild(string[] args)
    {
        var composition = new Composition(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")!=null);
        composition.ParseCommandLine(args);
        composition.RunCommand();
    }
    
    public static bool RunBuild(
        IFsAbstraction files,
        string typeScriptVersion,
        string directory,
        out string javaScript,
        out string parsedMessages)
    {
        javaScript = null;
        parsedMessages = null;
        
        var context = CreateBundlingContext(files, directory, typeScriptVersion);
        PrepareBundlingContext(context, directory);
        
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
    
    private static void PrepareBundlingContext(BuildContext context, string directory)
    {
        context.DiskCache = new DiskCache(context.FileSystemAbstraction, () => new DummyWatcher());
        context.MainBuildResult = new MainBuildResult(true, null, null);
        context.DirectoryCache = context.DiskCache.TryGetItem(directory) as IDirectoryCache;
        context.TsProject = TSProject.Create(context.DirectoryCache, context.DiskCache, Logger, null);
        context.TsProject!.IsRootProject = true;
        context.TsProject.ProjectOptions!.ForbiddenDependencyUpdate = true;
        context.TsProject.ProjectOptions!.UpdateFromProjectJson(null);
        context.TsProject.ProjectOptions.Debug = false;
        context.TsProject.ProjectOptions.LibraryMode = true;
        context.TsProject.ProjectOptions.RefreshCompilerOptions();
        context.TsProject.ProjectOptions.CompilerOptions!.allowJs = true;
        context.TsProject.ProjectOptions.CompilerOptions.declaration = true;
        context.TsProject.ProjectOptions.CompilerOptions.module = ModuleKind.Commonjs;
        context.TsProject.ProjectOptions.CompilerOptions.target = ScriptTarget.Es2019;
        context.TsProject.ProjectOptions.CompilerOptions.strict = true;
        context.TsProject.ProjectOptions.CompilerOptions.outDir = "dist";
        context.TsProject.ProjectOptions.CompilerOptions.moduleResolution = ModuleResolutionKind.NodeJs;
        context.TsProject.ProjectOptions.RefreshMainFile();
        context.BuildResult = new BuildResult(context.MainBuildResult, context.TsProject.ProjectOptions);
        context.Messages = new List<Diagnostic>();
        context.BuildCache = new DummyBuildCache();
        context.TranspilationContext = new BuildCtx(
            new CompilerPool(context.ToolsDir, Logger),
            context.DiskCache,
            verbose: true,
            Logger,
            currentDirectory: context.TsProject.Owner.FullPath,
            context.BuildCache,
            typeCheckValue: "no");
    }
    
    [GeneratedRegex(@".*/(.*?)$")]
    private static partial Regex FileNameRegex();

    private static string GetFileName(string path)
    {
        var match = FileNameRegex().Match(path);
        return !match.Success ? "" : string.Concat(match.Groups[1].Value);
    }
    
    private static string ParseMessages(IList<Diagnostic> messages, bool onlySemantic = false)
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
    
    private static void Bundle(BuildContext context)
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
            buildSourceMap: false,
            sourceMapSourceRoot: null);
    }
        
    private static BuildContext CreateBundlingContext(
        IFsAbstraction files,
        string directory,
        string typeScriptVersion)
    {
        var context = new BuildContext
        {
            ToolsDir = new ToolsDir(directory, Logger, files),
            FileSystemAbstraction = files,
        };
        context.ToolsDir.SetTypeScriptVersion(typeScriptVersion);
        return context;
    }
    
    private static void Transpile(
        BuildContext context,
        ref int errors,
        ref int warnings)   
    {
        context.TranspilationContext.Build(
            context.TsProject.ProjectOptions!,
            buildOnlyOnce: true,
            context.BuildResult,
            context.MainBuildResult,
            iterationId: 1);
        
        IncludeMessages(
            context.TsProject.ProjectOptions,
            context.MainBuildResult,
            context.BuildResult,
            ref errors,
            ref warnings,
            context.Messages);
    }
    
    private static void IncludeMessages(
        ProjectOptions options,
        MainBuildResult mainBuildResult,
        BuildResult buildResult,
        ref int errors,
        ref int warnings,
        IList<Diagnostic> messages)
    {
        var rootPath = options.Owner.Owner.FullPath;
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
                    FileName = PathUtils.ForDiagnosticDisplay(pathInfoPair.Key,
                        mainBuildResult.CommonSourceDirectory ?? rootPath, mainBuildResult.CommonSourceDirectory),
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