using Markdig.Extensions.TaskLists;

namespace BobrilMdx;

public class TsxTaskListRenderer : TsxObjectRenderer<TaskList>
{
    protected override void Write(TsxRenderer renderer, TaskList obj)
    {
        if (renderer.EnableHtmlForInline)
        {
            renderer.Write("<mdx.Task").WriteProps(obj).Write(" done={");
            renderer.Write(obj.Checked ? "true" : "false");
            renderer.Write("} />");
        }
        else
        {
            renderer.Write('[');
            renderer.Write(obj.Checked ? "x" : " ");
            renderer.Write(']');
        }
    }
}