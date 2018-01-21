using JavaScriptEngineSwitcher.Core;

namespace Lib.ToolsDir
{
    public interface IToolsDir
    {
        string Path { get; }
        string GetTypeScriptVersion();
        void InstallTypeScriptVersion(string version = "*");
        void RunYarn(string dir, string aParams);
        void UpdateDependencies(string dir, bool upgrade, string npmRegistry);
        string TypeScriptLibDir { get; }
        string TypeScriptJsContent { get; }
        string LoaderJs { get; }
        string JasmineCoreJs { get; }
        string JasmineBootJs { get; }
        string JasmineDts { get; }
        string WebtIndexHtml { get; }
        string WebtAJs { get; }
        string WebIndexHtml { get; }
        string WebAJs { get; }
        string JasmineDtsPath { get; }
        string GetLocaleDef(string locale);
        string TsLibSource { get; }
        string ImportSource { get;  }
        IJsEngine CreateJsEngine();
    }
}
