using JavaScriptEngineSwitcher.Core;

namespace Lib.ToolsDir
{
    public interface IToolsDir
    {
        string Path { get; }
        string GetTypeScriptVersion();
        void InstallTypeScriptVersion(string version = "*");
        void RunYarn(string dir, string aParams);
        string TypeScriptLibDir { get; }
        string TypeScriptJsContent { get; }
        string LoaderJs { get; }

        IJsEngine CreateJsEngine();
    }
}
