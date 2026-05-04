using Lib.Translation;
using Xunit;

namespace Lib.Test;

public class TranslationDbTests
{
    [Fact]
    public void RestoreCustomFormatters_SingleQuoted()
    {
        var original = "The dispatch of change set {changeSetName, quoted}to the {environmentName, quoted}environment has failed.";
        var target = "Odeslani sady zmen {changeSetName} do prostredi {environmentName} selhalo.";
        var result = TranslationDb.RestoreCustomFormatters(original, target);
        Assert.Equal("Odeslani sady zmen {changeSetName, quoted}do prostredi {environmentName, quoted}selhalo.", result);
    }

    [Fact]
    public void RestoreCustomFormatters_MultipleSameFormatter()
    {
        var original = "{name, quoted} and {city, quoted}";
        var target = "{name} a {city}";
        var result = TranslationDb.RestoreCustomFormatters(original, target);
        Assert.Equal("{name, quoted}a {city, quoted}", result);
    }

    [Fact]
    public void RestoreCustomFormatters_NoCustomFormatters_ReturnsTargetUnchanged()
    {
        var original = "Hello {name} welcome to {place}";
        var target = "Ahoj {name} vitej v {place}";
        var result = TranslationDb.RestoreCustomFormatters(original, target);
        Assert.Equal(target, result);
    }

    [Fact]
    public void RestoreCustomFormatters_StandardIcuFormattersAreIgnored()
    {
        var original = "The count is {count, number} and date is {today, date}";
        var target = "Pocet je {count} a datum je {today}";
        var result = TranslationDb.RestoreCustomFormatters(original, target);
        Assert.Equal(target, result);
    }

    [Fact]
    public void RestoreCustomFormatters_TargetMissingParameter_Unchanged()
    {
        var original = "{name, quoted} and {city, space}";
        var target = "{name} a mesto";
        var result = TranslationDb.RestoreCustomFormatters(original, target);
        Assert.Equal("{name, quoted}a mesto", result);
    }

    [Fact]
    public void RestoreCustomFormatters_EmptyOriginal_ReturnsTargetUnchanged()
    {
        var target = "Hello {name}";
        var result = TranslationDb.RestoreCustomFormatters("", target);
        Assert.Equal(target, result);
    }

    [Fact]
    public void RestoreCustomFormatters_TrailingSpaceIsConsumed()
    {
        var original = "{val, quoted}";
        var target = "{val} text";
        var result = TranslationDb.RestoreCustomFormatters(original, target);
        Assert.Equal("{val, quoted}text", result);
    }

    [Fact]
    public void NormalizeMessageForExport_StripsQuotedAndAddsSpace()
    {
        var message = "The dispatch of change set {changeSetName, quoted}to the {environmentName, quoted}environment has failed.";
        var result = TranslationDb.NormalizeMessageForExport(message);
        Assert.Equal("The dispatch of change set {changeSetName} to the {environmentName} environment has failed.", result);
    }

    [Fact]
    public void NormalizeMessageForExport_StripsSpaceAndAddsSpace()
    {
        var message = "The {name, space}room is full.";
        var result = TranslationDb.NormalizeMessageForExport(message);
        Assert.Equal("The {name} room is full.", result);
    }

    [Fact]
    public void NormalizeMessageForExport_PreservesStandardFormatters()
    {
        var message = "Count: {count, number} on {today, date}";
        var result = TranslationDb.NormalizeMessageForExport(message);
        Assert.Equal(message, result);
    }

    [Fact]
    public void NormalizeMessageForExport_NoCustomFormatters_ReturnsUnchanged()
    {
        var message = "Hello {name}, welcome to {place}";
        var result = TranslationDb.NormalizeMessageForExport(message);
        Assert.Equal(message, result);
    }

    [Fact]
    public void NormalizeMessageForExport_QuotedAtEnd()
    {
        var message = "Value is {val, quoted}";
        var result = TranslationDb.NormalizeMessageForExport(message);
        Assert.Equal("Value is {val} ", result);
    }
}
