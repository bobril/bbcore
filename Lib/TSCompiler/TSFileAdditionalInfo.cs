﻿using Lib.DiskCache;
using Njsast;
using Njsast.Bobril;
using Njsast.SourceMap;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;

namespace Lib.TSCompiler;

public enum FileCompilationType
{
    Unknown,
    TypeScript,
    EsmJavaScript,
    JavaScript,
    Css,
    ImportedCss,
    Resource,
    JavaScriptAsset,
    Json,
    TypeScriptDefinition,
    Mdxb,
    MdxbList,
    Scss
}

public class DependencyTriplet
{
    public byte[]? SourceHash { get; set; }
    public string? Import { get; set; }
    public byte[]? TargetHash { get; set; }
}

public class TsFileAdditionalInfo
{
    public FileCompilationType Type;
    public IFileCache? Owner { get; set; }
    public IDirectoryCache? DirOwner { get; set; }

    public string? Output { get; set; }
    public SourceMap? MapLink { get; set; }
    public SourceInfo? SourceInfo { get; set; }
    public List<DependencyTriplet>? TranspilationDependencies { get; set; }
    public Image<Rgba32>? Image { get; set; }

    public int ImageCacheId;
    public string? OutputUrl { get; set; }
    public TSProject? FromModule;

    public bool TakenFromBuildCache;

    TsFileAdditionalInfo()
    {
    }

    public void StartCompiling()
    {
        Diagnostics.Clear();
        Dependencies.Clear();
        TranspilationDependencies = null;
    }

    public StructList<string> Dependencies;
    public StructList<Diagnostic> Diagnostics;

    public int IterationId;
    internal int ChangeId;
    internal bool HasError;

    public TSProject? FromModuleRefresh
    {
        get
        {
            if (FromModule is { IsRootProject: false }) return FromModule;
            var dir = Owner?.Parent;
            IDirectoryCache? moduleDir = null;
            while (dir is { Project: null })
            {
                if (dir.Parent?.Name == "node_modules")
                {
                    moduleDir = dir;
                }

                if ((dir.Parent?.Name.StartsWith("@") ?? false) && dir.Parent?.Parent?.Name == "node_modules")
                {
                    moduleDir = dir;
                }

                dir = dir.Parent;
            }

            FromModule = dir?.Project as TSProject;

            if (moduleDir != null && (FromModule?.IsRootProject ?? false))
            {
                FromModule = TSProject.Create(moduleDir, FromModule.DiskCache, FromModule.Logger, null);
            }
            return FromModule;
        }
    }

    public void ReportDependency(string fullname)
    {
        if (Dependencies.IndexOf(fullname) >= 0) return;
        Dependencies.Add(fullname);
    }

    public void ReportTranspilationDependency(byte[] sourceHash, string import, byte[] targetHash)
    {
        TranspilationDependencies ??= new();

        foreach (var dep in TranspilationDependencies)
        {
            if (dep.Import == import && dep.SourceHash.AsSpan().SequenceEqual(sourceHash))
                return;
        }

        TranspilationDependencies.Add(new()
        {
            SourceHash = sourceHash,
            Import = import,
            TargetHash = targetHash
        });
    }

    public void ReportDiag(bool isError, int code, string text, int startLine, int startCharacter, int endLine,
        int endCharacter)
    {
        Diagnostics.Add(new Diagnostic
        {
            IsError = isError,
            Code = code,
            Text = text,
            FileName = Owner!.FullPath,
            StartLine = startLine,
            StartCol = startCharacter,
            EndLine = endLine,
            EndCol = endCharacter
        });
    }

    public static TsFileAdditionalInfo? Create(IFileCache? file)
    {
        if (file == null) return null;
        var dir = file.Parent;
        while (dir is { Project: null })
        {
            dir = dir.Parent;
        }

        return new() {Owner = file, FromModule = dir?.Project as TSProject};
    }

    public static TsFileAdditionalInfo CreateVirtual(IDirectoryCache? inDir)
    {
        var dir = inDir;
        while (dir is { Project: null })
        {
            dir = dir.Parent;
        }

        return new() {Owner = null, FromModule = dir?.Project as TSProject};
    }

    internal void ReportDiag(List<Diagnostic> diagnostics)
    {
        foreach (var diag in diagnostics)
        {
            Diagnostics.Add(diag);
        }
    }

    internal bool DetectChange()
    {
        if (Type == FileCompilationType.MdxbList) return true;
        var res = Owner.ChangeId != ChangeId;
        ChangeId = Owner.ChangeId;
        return res;
    }
}
