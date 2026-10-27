using System;
using System.Collections.Generic;
using System.Linq;
using Njsast;
using Xunit;

namespace Test;

public class StructRefListTest
{
    [Fact]
    public void AppendingRangesPreservesItemsAcrossGrowthAndSelfAppend()
    {
        StructRefList<string> list = [];
        List<string> expected = [];
        for (var i = 0; i < 64; i++)
        {
            string[] range = [i.ToString(), (i + 1).ToString()];
            list.AddRange(range);
            expected.AddRange(range);
        }

        list.AddRange(list.AsReadOnlySpan());
        expected.AddRange(expected.ToArray());
        Assert.Equal(expected, list.ToArray());
        list.Add("last");
        expected.Add("last");
        Assert.Equal(expected, list.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AppendingRangesHandlesSmallLists(int initialCount)
    {
        var initial = Enumerable.Range(0, initialCount).Select(i => i.ToString()).ToArray();
        var list = new StructRefList<string>(initial);
        list.AddRange(ReadOnlySpan<string>.Empty);
        list.AddRange(new[] { "a", "b" });
        list.Add("c");
        Assert.Equal(initial.Concat(["a", "b", "c"]), list.ToArray());
    }

    [Fact]
    public void RepeatedRangeAppendsHaveLinearAllocationCost()
    {
        // An exact-size reallocation per append would allocate more than 30 MB here.
        // Geometric growth needs less than 100 KB, independently of machine speed.
        string[] range = ["a", "b"];
        StructRefList<string> list = [];
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2048; i++) list.AddRange(range);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(4096u, list.Count);
        Assert.True(allocated < 200_000, $"Range appends allocated {allocated} bytes.");
    }
}
