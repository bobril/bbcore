namespace Njsast.Ast
{
    public class JsModuleExport
    {
        public readonly string ModuleName;
        public readonly string ExportName;

        public JsModuleExport(string moduleName, string exportName)
        {
            ModuleName = moduleName;
            ExportName = exportName;
        }
    }
}