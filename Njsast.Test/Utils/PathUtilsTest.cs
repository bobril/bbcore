using Njsast.Utils;
using Xunit;

namespace Test.Utils;

public class PathUtilsTest
{
    [Theory]
    [InlineData("abc", ".js", "abc.js")]
    [InlineData("abc", "js", "abc.js")]
    [InlineData("abc.txt", ".js", "abc.js")]
    [InlineData("abc.txt", "js", "abc.js")]
    [InlineData("abc.txt", "docx", "abc.docx")]
    [InlineData("abc.longExtenssion", "docx", "abc.docx")]
    [InlineData("C:/Hello/World/abc", "docx", "C:/Hello/World/abc.docx")]
    [InlineData("C:/Hello/World/abc.txt", ".docx", "C:/Hello/World/abc.docx")]
    [InlineData("C:/Hello/World/abc.txt", "docx", "C:/Hello/World/abc.docx")]
    public void Method_ChangeExtension_ShouldChangeExtension(string fileName, string newExtension,
        string expectedResult)
    {
        Assert.Equal(expectedResult, PathUtils.ChangeExtension(fileName, newExtension));
    }

    [Theory]
    [InlineData("Input/ConstEval/import1.js", "Input/ConstEval")]
    [InlineData("Input/import1.js", "Input")]
    public void Method_Parent_ShouldReturnParentPath(string path, string expectedResult)
    {
        Assert.Equal(expectedResult, PathUtils.Parent(path));
    }

    [Theory]
    [InlineData("Input/ConstEval\\import1.js", "Input")]
    [InlineData("Input\\ConstEval\\import1.js", "")]
    public void Method_Parent_ShouldNotWorkCorrectlyWithNonNormalizedPaths(string path, string expectedResult)
    {
        Assert.Equal(expectedResult, PathUtils.Parent(path));
    }

    [Theory]
    [InlineData("Input/ConstEval/import1.js", "Input/ConstEval")]
    [InlineData("Input/ConstEval\\import1.js", "Input/ConstEval")]
    [InlineData("Input\\ConstEval\\import1.js", "Input/ConstEval")]
    public void Method_ParentSafe_ShouldWorkWithAnyKindOfPathAndReturnParentPath(string path,
        string expectedResult)
    {
        Assert.Equal(expectedResult, PathUtils.ParentSafe(path));
    }
}