using JavaScriptEngineSwitcher.Core;
using Microsoft.AspNetCore.Http;

namespace Lib.ToolsDir
{
    public interface IToolsDir
    {
        string Path { get; }
        void SetJasmineVersion(string version);
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
        string JasmineDtsPath { get; }
        string GetLocaleDef(string locale);
        IJsEngine CreateJsEngine();
        byte[] WebGet(string path);
        byte[] WebtGet(string path);
        void ProxyWeb(string url);
        void ProxyWebt(string url);
    }
}
