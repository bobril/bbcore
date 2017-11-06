namespace Lib.Composition
{
    public class CompilationResultMessage
    {
        public string FileName;
        public bool IsError;
        public string Text;
        /// startLine, startCol, endLine, endCol all one based
        public int[] Pos;
    }
}