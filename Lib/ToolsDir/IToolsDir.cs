using JavaScriptEngineSwitcher.Core;

namespace Lib.ToolsDir
{
    public interface IToolsDir
    {
        string Path { get; }
        void SetTypeScriptVersion(string version);
        void SetTypeScriptPath(string projectPath);
        string TypeScriptLibDir { get; }
        string TypeScriptVersion { get; }
        string TypeScriptJsContent { get; }
        string LoaderJs { get; }
        string LiveReloadJs { get; }
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
