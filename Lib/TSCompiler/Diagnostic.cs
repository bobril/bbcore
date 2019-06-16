namespace Lib.TSCompiler
{
    public class Diagnostic
    {
        public bool IsError;
        public int Code;
        public string Text;
        public string FileName;
        public int StartLine;
        public int StartCol;
        public int EndLine;
        public int EndCol;
    }
}
