namespace Njsast.SourceMap
{
    public interface ISourceReplacer
    {
        void Replace(int fromLine, int fromCol, int toLine, int toCol, string content);
        void Move(int fromLine, int fromCol, int toLine, int toCol, int placeLine, int placeCol);
        void Apply(ISourceAdder sourceAdder);
    }
}
