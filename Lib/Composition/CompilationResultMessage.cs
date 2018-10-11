namespace Lib.Composition
{
    public class CompilationResultMessage
    {
        public string FileName;
        public bool IsError;
        public int Code;
        public string Text;
        /// startLine, startCol, endLine, endCol all one based
        public int[] Pos;
    }
}
