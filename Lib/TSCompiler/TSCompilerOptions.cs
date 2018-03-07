using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public class TSCompilerOptions : ITSCompilerOptions
    {
        public bool? allowJs { get; set; }
        public bool? allowSyntheticDefaultImports { get; set; }
        public bool? allowUnreachableCode { get; set; }
        public bool? allowUnusedLabels { get; set; }
        public bool? alwaysStrict { get; set; }
        public string baseUrl { get; set; }
        public string charset { get; set; }
        public bool? checkJs { get; set; }
        public bool? declaration { get; set; }
        public string declarationDir { get; set; }
        public bool? disableSizeLimit { get; set; }
        public bool? downlevelIteration { get; set; }
        public bool? emitBOM { get; set; }
        public bool? emitDecoratorMetadata { get; set; }
        public bool? experimentalDecorators { get; set; }
        public bool? forceConsistentCasingInFileNames { get; set; }
        public bool? importHelpers { get; set; }
        public bool? inlineSourceMap { get; set; }
        public bool? inlineSources { get; set; }
        public bool? isolatedModules { get; set; }
        public JsxEmit? jsx { get; set; }
        public ISet<string> lib { get; set; }
        public string locale { get; set; }
        public string mapRoot { get; set; }
        public int? maxNodeModuleJsDepth { get; set; }
        public ModuleKind? module { get; set; }
        public ModuleResolutionKind? moduleResolution { get; set; }
        public NewLineKind? newLine { get; set; }
        public bool? noEmit { get; set; }
        public bool? noEmitHelpers { get; set; }
        public bool? noEmitOnError { get; set; }
        public bool? noErrorTruncation { get; set; }
        public bool? noFallthroughCasesInSwitch { get; set; }
        public bool? noStrictGenericChecks { get; set; }
        public bool? noImplicitAny { get; set; }
        public bool? noImplicitReturns { get; set; }
        public bool? noImplicitThis { get; set; }
        public bool? noUnusedLocals { get; set; }
        public bool? noUnusedParameters { get; set; }
        public bool? noImplicitUseStrict { get; set; }
        public bool? noLib { get; set; }
        public bool? noResolve { get; set; }
        public string outDir { get; set; }
        public string outFile { get; set; }
        public IDictionary<string, IList<string>> paths { get; set; }
        public bool? preserveConstEnums { get; set; }
        public string project { get; set; }
        public string reactNamespace { get; set; }
        public string jsxFactory { get; set; }
        public bool? removeComments { get; set; }
        public string rootDir { get; set; }
        public IList<string> rootDirs { get; set; }
        public bool? skipLibCheck { get; set; }
        public bool? skipDefaultLibCheck { get; set; }
        public bool? sourceMap { get; set; }
        public string sourceRoot { get; set; }
        public bool? strict { get; set; }
        public bool? strictFunctionTypes { get; set; }
        public bool? strictNullChecks { get; set; }
        public bool? strictPropertyInitialization { get; set; }
        public bool? suppressExcessPropertyErrors { get; set; }
        public bool? suppressImplicitAnyIndexErrors { get; set; }
        public ScriptTarget? target { get; set; }
        public bool? traceResolution { get; set; }
        public IList<string> types { get; set; }
        public IList<string> typeRoots { get; set; }

        static public TSCompilerOptions Parse(JToken jToken)
        {
            if (jToken == null) return new TSCompilerOptions();
            return jToken.ToObject<TSCompilerOptions>();
        }

        public TSCompilerOptions Clone()
        {
            return new TSCompilerOptions
            {
                allowJs = this.allowJs,
                allowSyntheticDefaultImports = this.allowSyntheticDefaultImports,
                allowUnreachableCode = this.allowUnreachableCode,
                allowUnusedLabels = this.allowUnusedLabels,
                alwaysStrict = this.alwaysStrict,
                baseUrl = this.baseUrl,
                charset = this.charset,
                checkJs = this.checkJs,
                declaration = this.declaration,
                declarationDir = this.declarationDir,
                disableSizeLimit = this.disableSizeLimit,
                downlevelIteration = this.downlevelIteration,
                emitBOM = this.emitBOM,
                emitDecoratorMetadata = this.emitDecoratorMetadata,
                experimentalDecorators = this.experimentalDecorators,
                forceConsistentCasingInFileNames = this.forceConsistentCasingInFileNames,
                importHelpers = this.importHelpers,
                inlineSourceMap = this.inlineSourceMap,
                inlineSources = this.inlineSources,
                isolatedModules = this.isolatedModules,
                jsx = this.jsx,
                jsxFactory = this.jsxFactory,
                lib = this.lib,
                locale = this.locale,
                mapRoot = this.mapRoot,
                maxNodeModuleJsDepth = this.maxNodeModuleJsDepth,
                module = this.module,
                moduleResolution = this.moduleResolution,
                newLine = this.newLine,
                noEmit = this.noEmit,
                noEmitHelpers = this.noEmitHelpers,
                noEmitOnError = this.noEmitOnError,
                noErrorTruncation = this.noErrorTruncation,
                noFallthroughCasesInSwitch = this.noFallthroughCasesInSwitch,
                noStrictGenericChecks = this.noStrictGenericChecks,
                noImplicitAny = this.noImplicitAny,
                noImplicitReturns = this.noImplicitReturns,
                noImplicitThis = this.noImplicitThis,
                noImplicitUseStrict = this.noImplicitUseStrict,
                noLib = this.noLib,
                noResolve = this.noResolve,
                noUnusedLocals = this.noUnusedLocals,
                noUnusedParameters = this.noUnusedParameters,
                outDir = this.outDir,
                outFile = this.outFile,
                paths = this.paths,
                preserveConstEnums = this.preserveConstEnums,
                project = this.project,
                reactNamespace = this.reactNamespace,
                removeComments = this.removeComments,
                rootDir = this.rootDir,
                rootDirs = this.rootDirs,
                skipDefaultLibCheck = this.skipDefaultLibCheck,
                skipLibCheck = this.skipLibCheck,
                sourceMap = this.sourceMap,
                sourceRoot = this.sourceRoot,
                strict = this.strict,
                strictFunctionTypes = this.strictFunctionTypes,
                strictNullChecks = this.strictNullChecks,
                strictPropertyInitialization = this.strictPropertyInitialization,
                suppressExcessPropertyErrors = this.suppressExcessPropertyErrors,
                suppressImplicitAnyIndexErrors = this.suppressImplicitAnyIndexErrors,
                target = this.target,
                traceResolution = this.traceResolution,
                typeRoots = this.typeRoots,
                types = this.types
            };
        }
    }
}
