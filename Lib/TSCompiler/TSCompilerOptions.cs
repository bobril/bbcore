using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;

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
        public bool? resolveJsonModule { get; set; }

        static public TSCompilerOptions Parse(JToken jToken)
        {
            if (jToken == null) return new TSCompilerOptions();
            return jToken.ToObject<TSCompilerOptions>();
        }

        static readonly JsonSerializerSettings _cachedSerializerSettings;

        static TSCompilerOptions()
        {
            var res = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            res.Converters.Add(
                new Newtonsoft.Json.Converters.StringEnumConverter(new CamelCaseNamingStrategy(true, false), false));
            _cachedSerializerSettings = res;
        }

        static public JsonSerializerSettings GetSerializerSettings()
        {
            return _cachedSerializerSettings;
        }

        public ITSCompilerOptions Clone()
        {
            return new TSCompilerOptions
            {
                allowJs = allowJs,
                allowSyntheticDefaultImports = allowSyntheticDefaultImports,
                allowUnreachableCode = allowUnreachableCode,
                allowUnusedLabels = allowUnusedLabels,
                alwaysStrict = alwaysStrict,
                baseUrl = baseUrl,
                charset = charset,
                checkJs = checkJs,
                declaration = declaration,
                declarationDir = declarationDir,
                disableSizeLimit = disableSizeLimit,
                downlevelIteration = downlevelIteration,
                emitBOM = emitBOM,
                emitDecoratorMetadata = emitDecoratorMetadata,
                experimentalDecorators = experimentalDecorators,
                forceConsistentCasingInFileNames = forceConsistentCasingInFileNames,
                importHelpers = importHelpers,
                inlineSourceMap = inlineSourceMap,
                inlineSources = inlineSources,
                isolatedModules = isolatedModules,
                jsx = jsx,
                jsxFactory = jsxFactory,
                lib = lib,
                locale = locale,
                mapRoot = mapRoot,
                maxNodeModuleJsDepth = maxNodeModuleJsDepth,
                module = module,
                moduleResolution = moduleResolution,
                newLine = newLine,
                noEmit = noEmit,
                noEmitHelpers = noEmitHelpers,
                noEmitOnError = noEmitOnError,
                noErrorTruncation = noErrorTruncation,
                noFallthroughCasesInSwitch = noFallthroughCasesInSwitch,
                noStrictGenericChecks = noStrictGenericChecks,
                noImplicitAny = noImplicitAny,
                noImplicitReturns = noImplicitReturns,
                noImplicitThis = noImplicitThis,
                noImplicitUseStrict = noImplicitUseStrict,
                noLib = noLib,
                noResolve = noResolve,
                noUnusedLocals = noUnusedLocals,
                noUnusedParameters = noUnusedParameters,
                outDir = outDir,
                outFile = outFile,
                paths = paths,
                preserveConstEnums = preserveConstEnums,
                project = project,
                reactNamespace = reactNamespace,
                removeComments = removeComments,
                rootDir = rootDir,
                rootDirs = rootDirs,
                skipDefaultLibCheck = skipDefaultLibCheck,
                skipLibCheck = skipLibCheck,
                sourceMap = sourceMap,
                sourceRoot = sourceRoot,
                strict = strict,
                strictFunctionTypes = strictFunctionTypes,
                strictNullChecks = strictNullChecks,
                strictPropertyInitialization = strictPropertyInitialization,
                suppressExcessPropertyErrors = suppressExcessPropertyErrors,
                suppressImplicitAnyIndexErrors = suppressImplicitAnyIndexErrors,
                target = target,
                traceResolution = traceResolution,
                typeRoots = typeRoots,
                types = types,
                resolveJsonModule = resolveJsonModule
            };
        }

        public ITSCompilerOptions Merge(ITSCompilerOptions withInterface)
        {
            var with = (TSCompilerOptions)withInterface;
            if (with == null)
                return this;
            if (with.allowJs != null)
                allowJs = with.allowJs;
            if (with.allowSyntheticDefaultImports != null)
                allowSyntheticDefaultImports = with.allowSyntheticDefaultImports;
            if (with.allowUnreachableCode != null)
                allowUnreachableCode = with.allowUnreachableCode;
            if (with.allowUnusedLabels != null)
                allowUnusedLabels = with.allowUnusedLabels;
            if (with.alwaysStrict != null)
                alwaysStrict = with.alwaysStrict;
            if (with.baseUrl != null)
                baseUrl = with.baseUrl;
            if (with.charset != null)
                charset = with.charset;
            if (with.checkJs != null)
                checkJs = with.checkJs;
            if (with.declaration != null)
                declaration = with.declaration;
            if (with.declarationDir != null)
                declarationDir = with.declarationDir;
            if (with.disableSizeLimit != null)
                disableSizeLimit = with.disableSizeLimit;
            if (with.downlevelIteration != null)
                downlevelIteration = with.downlevelIteration;
            if (with.emitBOM != null)
                emitBOM = with.emitBOM;
            if (with.emitDecoratorMetadata != null)
                emitDecoratorMetadata = with.emitDecoratorMetadata;
            if (with.experimentalDecorators != null)
                experimentalDecorators = with.experimentalDecorators;
            if (with.forceConsistentCasingInFileNames != null)
                forceConsistentCasingInFileNames = with.forceConsistentCasingInFileNames;
            if (with.importHelpers != null)
                importHelpers = with.importHelpers;
            if (with.inlineSourceMap != null)
                inlineSourceMap = with.inlineSourceMap;
            if (with.inlineSources != null)
                inlineSources = with.inlineSources;
            if (with.isolatedModules != null)
                isolatedModules = with.isolatedModules;
            if (with.jsx != null)
                jsx = with.jsx;
            if (with.jsxFactory != null)
                jsxFactory = with.jsxFactory;
            if (with.lib != null)
                lib = with.lib;
            if (with.locale != null)
                locale = with.locale;
            if (with.mapRoot != null)
                mapRoot = with.mapRoot;
            if (with.maxNodeModuleJsDepth != null)
                maxNodeModuleJsDepth = with.maxNodeModuleJsDepth;
            if (with.module != null)
                module = with.module;
            if (with.moduleResolution != null)
                moduleResolution = with.moduleResolution;
            if (with.newLine != null)
                newLine = with.newLine;
            if (with.noEmit != null)
                noEmit = with.noEmit;
            if (with.noEmitHelpers != null)
                noEmitHelpers = with.noEmitHelpers;
            if (with.noEmitOnError != null)
                noEmitOnError = with.noEmitOnError;
            if (with.noErrorTruncation != null)
                noErrorTruncation = with.noErrorTruncation;
            if (with.noFallthroughCasesInSwitch != null)
                noFallthroughCasesInSwitch = with.noFallthroughCasesInSwitch;
            if (with.noImplicitAny != null)
                noImplicitAny = with.noImplicitAny;
            if (with.noImplicitReturns != null)
                noImplicitReturns = with.noImplicitReturns;
            if (with.noImplicitThis != null)
                noImplicitThis = with.noImplicitThis;
            if (with.noImplicitUseStrict != null)
                noImplicitUseStrict = with.noImplicitUseStrict;
            if (with.noLib != null)
                noLib = with.noLib;
            if (with.noResolve != null)
                noResolve = with.noResolve;
            if (with.noStrictGenericChecks != null)
                noStrictGenericChecks = with.noStrictGenericChecks;
            if (with.noUnusedLocals != null)
                noUnusedLocals = with.noUnusedLocals;
            if (with.noUnusedParameters != null)
                noUnusedParameters = with.noUnusedParameters;
            if (with.outDir != null)
                outDir = with.outDir;
            if (with.outFile != null)
                outFile = with.outFile;
            if (with.paths != null)
                paths = with.paths;
            if (with.preserveConstEnums != null)
                preserveConstEnums = with.preserveConstEnums;
            if (with.project != null)
                project = with.project;
            if (with.reactNamespace != null)
                reactNamespace = with.reactNamespace;
            if (with.removeComments != null)
                removeComments = with.removeComments;
            if (with.rootDir != null)
                rootDir = with.rootDir;
            if (with.rootDirs != null)
                rootDirs = with.rootDirs;
            if (with.skipDefaultLibCheck != null)
                skipDefaultLibCheck = with.skipDefaultLibCheck;
            if (with.skipLibCheck != null)
                skipLibCheck = with.skipLibCheck;
            if (with.sourceMap != null)
                sourceMap = with.sourceMap;
            if (with.sourceRoot != null)
                sourceRoot = with.sourceRoot;
            if (with.strict != null)
                strict = with.strict;
            if (with.strictFunctionTypes != null)
                strictFunctionTypes = with.strictFunctionTypes;
            if (with.strictNullChecks != null)
                strictNullChecks = with.strictNullChecks;
            if (with.strictPropertyInitialization != null)
                strictPropertyInitialization = with.strictPropertyInitialization;
            if (with.suppressExcessPropertyErrors != null)
                suppressExcessPropertyErrors = with.suppressExcessPropertyErrors;
            if (with.suppressImplicitAnyIndexErrors != null)
                suppressImplicitAnyIndexErrors = with.suppressImplicitAnyIndexErrors;
            if (with.target != null)
                target = with.target;
            if (with.traceResolution != null)
                traceResolution = with.traceResolution;
            if (with.typeRoots != null)
                typeRoots = with.typeRoots;
            if (with.types != null)
                types = with.types;
            if (with.resolveJsonModule != null)
                resolveJsonModule = with.resolveJsonModule;
            return this;
        }
    }
}
