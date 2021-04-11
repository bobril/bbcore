namespace BobrilMdx
{
    public class MdxCodeInlineRenderer : TsxObjectRenderer<MdxCodeInline>
    {
        protected override void Write(TsxRenderer renderer, MdxCodeInline obj)
        {
            renderer.Write('{').Write(obj.Content).Write('}');
        }
    }
}