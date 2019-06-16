namespace Njsast.SourceMap
{
    public interface ISourceReplacer
    {
        void Replace(int fromLine, int fromCol, int toLine, int toCol, string content);
        void Apply(ISourceAdder sourceAdder);
    }
}
