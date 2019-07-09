using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Lib.TSCompiler
{
    public enum ModuleKind
    {
        None = 0,
        Commonjs = 1,
        Amd = 2,
        Umd = 3,
        System = 4,
        Es2015 = 5,
    }

    public enum JsxEmit
    {
        None = 0,
        Preserve = 1,
        React = 2,
        [EnumMember(Value = "react-native")]
        ReactNative = 3,
    }

    public enum ModuleResolutionKind
    {
        Classic = 1,
        Nodejs = 2,
    }

    public enum NewLineKind
    {
        CarriageReturnLineFeed = 0,
        LineFeed = 1,
    }

    public enum ScriptTarget
    {
        Es3 = 0,
        Es5 = 1,
        Es2015 = 2,
        Es2016 = 3,
        Es2017 = 4,
        Es2018 = 5,
        Esnext = 6,
    }

    public interface ITSCompilerOptions
    {
        bool? allowJs { get; set; }
        bool? allowSyntheticDefaultImports { get; set; }
        bool? allowUnreachableCode { get; set; }
        bool? allowUnusedLabels { get; set; }
        bool? alwaysStrict { get; set; }
        string baseUrl { get; set; }
        string charset { get; set; }
        bool? checkJs { get; set; }
        bool? declaration { get; set; }
        string declarationDir { get; set; }
        bool? disableSizeLimit { get; set; }
        bool? downlevelIteration { get; set; }
        bool? emitBOM { get; set; }
        bool? emitDecoratorMetadata { get; set; }
        bool? experimentalDecorators { get; set; }
        bool? forceConsistentCasingInFileNames { get; set; }
        bool? importHelpers { get; set; }
        bool? inlineSourceMap { get; set; }
        bool? inlineSources { get; set; }
        bool? isolatedModules { get; set; }
        JsxEmit? jsx { get; set; }
        ISet<string> lib { get; set; }
        string locale { get; set; }
        string mapRoot { get; set; }
        int? maxNodeModuleJsDepth { get; set; }
        ModuleKind? module { get; set; }
        ModuleResolutionKind? moduleResolution { get; set; }
        NewLineKind? newLine { get; set; }
        bool? noEmit { get; set; }
        bool? noEmitHelpers { get; set; }
        bool? noEmitOnError { get; set; }
        bool? noErrorTruncation { get; set; }
        bool? noFallthroughCasesInSwitch { get; set; }
        bool? noImplicitAny { get; set; }
        bool? noImplicitReturns { get; set; }
        bool? noImplicitThis { get; set; }
        bool? noUnusedLocals { get; set; }
        bool? noUnusedParameters { get; set; }
        bool? noImplicitUseStrict { get; set; }
        bool? noLib { get; set; }
        bool? noResolve { get; set; }
        string outDir { get; set; }
        string outFile { get; set; }
        IDictionary<string, IList<string>> paths { get; set; }
        bool? preserveConstEnums { get; set; }
        string project { get; set; }
        string reactNamespace { get; set; }
        string jsxFactory { get; set; }
        bool? removeComments { get; set; }
        string rootDir { get; set; }
        IList<string> rootDirs { get; set; }
        bool? skipLibCheck { get; set; }
        bool? skipDefaultLibCheck { get; set; }
        bool? sourceMap { get; set; }
        string sourceRoot { get; set; }
        bool? strict { get; set; }
        bool? strictNullChecks { get; set; }
        bool? suppressExcessPropertyErrors { get; set; }
        bool? suppressImplicitAnyIndexErrors { get; set; }
        ScriptTarget? target { get; set; }
        bool? traceResolution { get; set; }
        IList<string> types { get; set; }
        /** Paths used to compute primary types search locations */
        IList<string> typeRoots { get; set; }

        ITSCompilerOptions Clone();
        ITSCompilerOptions Merge(ITSCompilerOptions withInterface);
    }
}
