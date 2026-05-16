using System;
using System.Collections.Generic;
using Njsast;
using Xunit;

namespace Test;

public class StructListTest
{
    [Fact]
    public void ShouldRemoveItemFromListBasedOnObject()
    {
        var list = GetNewList(10);
        var item = list[3];
        Assert.Equal(10, (int)list.Count);
        Assert.Equal(item, list[3]);
        Assert.Equal(3, list[3].Value);
        list.RemoveItem(item);
        Assert.Equal(9, (int)list.Count);
        Assert.NotEqual(item, list[3]);
        Assert.Equal(4, list[3].Value);
    }

    [Fact]
    public void ShouldRemoveItemFromListBasedOnIndex()
    {
        var list = GetNewList(10);
        var item = list[3];
        Assert.Equal(10, (int)list.Count);
        Assert.Equal(item, list[3]);
        Assert.Equal(3, list[3].Value);
        list.RemoveAt(3);
        Assert.Equal(9, (int)list.Count);
        Assert.NotEqual(item, list[3]);
        Assert.Equal(4, list[3].Value);
    }

    [Fact]
    public void RemoveAt_ShouldThrowIfIndexDoesNotExist()
    {
        var list = GetNewList(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(20));
    }

    [Fact]
    public void Indexer_ShouldThrowIfIndexDoesNotExist()
    {
        var list = GetNewList(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[20]);
    }

    [Fact]
    public void RemoveItem_ShouldThrowIfItemNotExistInCollection()
    {
        var list = GetNewList(10);
        var item = new DummyClass{Value = 60};
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveItem(item));
    }

    [Fact]
    public void ShouldNotAddItemIfItIsNotUnique()
    {
        var list = GetNewList(10);
        var item = list[3];
        Assert.Equal(10, (int)list.Count);
        var result = list.AddUnique(item);
        Assert.Equal(10, (int)list.Count);
        Assert.False(result);
    }

    [Fact]
    public void ShouldAddItemIfItIsUnique()
    {
        var list = GetNewList(10);
        Assert.Equal(10, (int)list.Count);
        var result = list.AddUnique(new DummyClass {Value = 60});
        Assert.Equal(11, (int)list.Count);
        Assert.Equal(60, list[10].Value);
        Assert.True(result);
    }

    [Fact]
    public void ShouldReplaceSingleItem()
    {
        var list = GetNewList(10);
        Assert.Equal(10, (int)list.Count);
        var originalItem = list[3];
        var newItem = new DummyClass {Value = 60};
        list.ReplaceItem(originalItem, newItem);
        Assert.Equal(10, (int)list.Count);
        Assert.Equal(newItem, list[3]);
    }

    [Fact]
    public void ShouldReplaceSingleItemPassedAsCollection()
    {
        var list = GetNewList(10);
        Assert.Equal(10, (int)list.Count);
        var newItem = new DummyClass {Value = 60};
        var replaceList = new StructList<DummyClass>();
        replaceList.Add(newItem);
        list.ReplaceItemAt(3, replaceList);
        Assert.Equal(10, (int)list.Count);
        Assert.Equal(newItem, list[3]);
        Assert.Equal(2, list[2].Value);
        Assert.Equal(60, list[3].Value);
        Assert.Equal(4, list[4].Value);
    }

    [Fact]
    public void ShouldRemoveItemIfReplaceCollectionIsEmpty()
    {
        var list = GetNewList(10);
        Assert.Equal(10, (int)list.Count);
        var replaceList = new StructList<DummyClass>();
        list.ReplaceItemAt(3, replaceList);
        Assert.Equal(9, (int)list.Count);
        Assert.Equal(2, list[2].Value);
        Assert.Equal(4, list[3].Value);
    }

    [Theory]
    [InlineData(10, 3, 3)] // First has more items
    [InlineData(13, 15, 9)] // Second has more items
    [InlineData(9, 9, 5)] // Same size collections
    [InlineData(11, 5, 10)] // Replace last item
    [InlineData(7, 6, 0)] // Replace first item
    public void ShouldReplaceItemWithCollection(uint items, uint newItems, uint replaceIndex)
    {
        var list = GetNewList(items);
        Assert.Equal(items, list.Count);
        var replaceList = GetNewList(newItems, 60);
        list.ReplaceItemAt((int)replaceIndex, replaceList);

        foreach (var (index, expectedValue) in GetExpectedIndexAndValue())
        {
            Assert.Equal(expectedValue, list[index].Value);
        }

        Assert.Equal(items + newItems -1, list.Count);

        IEnumerable<(uint, int)> GetExpectedIndexAndValue()
        {
            var originalCollectionValue = 0;
            var newCollectionValue = 60;
            for (uint index = 0; index < items + newItems - 1; index++)
            {
                if (index < replaceIndex)
                    yield return (index, originalCollectionValue++);
                else if (index < replaceIndex + newItems)
                    yield return (index, newCollectionValue++);
                else
                    yield return (index, ++originalCollectionValue);
            }
        }
    }

    [Fact]
    public void ReplaceItem_ShouldThrowIfItemDoesNotExistInCollection()
    {
        var list = GetNewList(10);
        var replaceList = new StructList<DummyClass>();
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ReplaceItemAt(11, replaceList));
    }

    static StructList<DummyClass> GetNewList(uint items, int valueOffset = 0)
    {
        var result = new StructList<DummyClass>();
        for (var i = 0; i < items; i++)
        {
            result.Add(new DummyClass {Value = i + valueOffset});
        }

        return result;
    }

    class DummyClass
    {
        public int Value { get; set; }
    }
}