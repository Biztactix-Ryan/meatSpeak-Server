using Xunit;

namespace MeatSpeak.Protocol.Tests;

public class IrcTagsTests
{
    // --- Parse tests ---

    [Fact]
    public void Parse_NullString_ReturnsEmptyDictionary()
    {
        var result = IrcTags.Parse(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyDictionary()
    {
        var result = IrcTags.Parse("");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleTagWithValue_ParsesCorrectly()
    {
        var result = IrcTags.Parse("time=2024-01-01");

        Assert.Single(result);
        Assert.Equal("2024-01-01", result["time"]);
    }

    [Fact]
    public void Parse_SingleTagWithoutValue_ValueIsNull()
    {
        var result = IrcTags.Parse("draft/reply");

        Assert.Single(result);
        Assert.True(result.ContainsKey("draft/reply"));
        Assert.Null(result["draft/reply"]);
    }

    [Fact]
    public void Parse_MultipleTags_ParsesAll()
    {
        var result = IrcTags.Parse("time=2024-01-01;msgid=abc123;batch=ref1");

        Assert.Equal(3, result.Count);
        Assert.Equal("2024-01-01", result["time"]);
        Assert.Equal("abc123", result["msgid"]);
        Assert.Equal("ref1", result["batch"]);
    }

    [Fact]
    public void Parse_EmptyValueAfterEquals_ReturnsEmptyString()
    {
        var result = IrcTags.Parse("key=");

        Assert.Single(result);
        Assert.Equal("", result["key"]);
    }

    [Fact]
    public void Parse_TrailingSemicolon_IgnoresEmptyEntry()
    {
        var result = IrcTags.Parse("key=value;");

        Assert.Single(result);
        Assert.Equal("value", result["key"]);
    }

    [Fact]
    public void Parse_LeadingSemicolon_IgnoresEmptyEntry()
    {
        var result = IrcTags.Parse(";key=value");

        Assert.Single(result);
        Assert.Equal("value", result["key"]);
    }

    [Fact]
    public void Parse_ConsecutiveSemicolons_IgnoresEmptyEntries()
    {
        var result = IrcTags.Parse("a=1;;;b=2");

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result["a"]);
        Assert.Equal("2", result["b"]);
    }

    [Fact]
    public void Parse_DuplicateKey_LastWins()
    {
        var result = IrcTags.Parse("key=first;key=second");

        Assert.Single(result);
        Assert.Equal("second", result["key"]);
    }

    // --- Unescape tests via Parse ---

    [Fact]
    public void Parse_EscapedSemicolon_UnescapesToSemicolon()
    {
        var result = IrcTags.Parse(@"key=hello\:world");

        Assert.Equal("hello;world", result["key"]);
    }

    [Fact]
    public void Parse_EscapedSpace_UnescapesToSpace()
    {
        var result = IrcTags.Parse(@"key=hello\sworld");

        Assert.Equal("hello world", result["key"]);
    }

    [Fact]
    public void Parse_EscapedBackslash_UnescapesToBackslash()
    {
        var result = IrcTags.Parse(@"key=hello\\world");

        Assert.Equal("hello\\world", result["key"]);
    }

    [Fact]
    public void Parse_EscapedCR_UnescapesToCR()
    {
        var result = IrcTags.Parse(@"key=hello\rworld");

        Assert.Equal("hello\rworld", result["key"]);
    }

    [Fact]
    public void Parse_EscapedLF_UnescapesToLF()
    {
        var result = IrcTags.Parse(@"key=hello\nworld");

        Assert.Equal("hello\nworld", result["key"]);
    }

    [Fact]
    public void Parse_UnknownEscapeSequence_PassesThroughChar()
    {
        var result = IrcTags.Parse(@"key=hello\xworld");

        Assert.Equal("helloxworld", result["key"]);
    }

    [Fact]
    public void Parse_TrailingBackslash_TreatedAsLiteral()
    {
        // A trailing backslash with no following character is appended as literal '\'
        var result = IrcTags.Parse("key=hello\\");

        Assert.Equal("hello\\", result["key"]);
    }

    [Fact]
    public void Parse_MultipleEscapesInOneValue_AllUnescaped()
    {
        // \: -> ; \s -> space \\ -> \ combined
        var result = IrcTags.Parse(@"key=a\:b\sc\\d");

        Assert.Equal("a;b c\\d", result["key"]);
    }

    // --- Serialize tests ---

    [Fact]
    public void Serialize_EmptyDictionary_ReturnsEmptyString()
    {
        var tags = new Dictionary<string, string?>();

        var result = IrcTags.Serialize(tags);

        Assert.Equal("", result);
    }

    [Fact]
    public void Serialize_SingleTagWithValue_FormatsCorrectly()
    {
        var tags = new Dictionary<string, string?> { ["time"] = "2024-01-01" };

        var result = IrcTags.Serialize(tags);

        Assert.Equal("time=2024-01-01", result);
    }

    [Fact]
    public void Serialize_SingleTagNullValue_OmitsEquals()
    {
        var tags = new Dictionary<string, string?> { ["draft/reply"] = null };

        var result = IrcTags.Serialize(tags);

        Assert.Equal("draft/reply", result);
    }

    [Fact]
    public void Serialize_ValueWithSemicolon_Escapes()
    {
        var tags = new Dictionary<string, string?> { ["key"] = "hello;world" };

        var result = IrcTags.Serialize(tags);

        Assert.Equal(@"key=hello\:world", result);
    }

    [Fact]
    public void Serialize_ValueWithSpace_Escapes()
    {
        var tags = new Dictionary<string, string?> { ["key"] = "hello world" };

        var result = IrcTags.Serialize(tags);

        Assert.Equal(@"key=hello\sworld", result);
    }

    [Fact]
    public void Serialize_ValueWithBackslash_Escapes()
    {
        var tags = new Dictionary<string, string?> { ["key"] = "hello\\world" };

        var result = IrcTags.Serialize(tags);

        Assert.Equal(@"key=hello\\world", result);
    }

    // --- Roundtrip ---

    [Fact]
    public void Roundtrip_ParseSerializeParse_PreservesValues()
    {
        var original = @"time=2024-01-01;msg=hello\sworld\:bye;flag";

        var parsed = IrcTags.Parse(original);
        var serialized = IrcTags.Serialize(parsed);
        var reparsed = IrcTags.Parse(serialized);

        Assert.Equal(parsed.Count, reparsed.Count);
        foreach (var (key, value) in parsed)
            Assert.Equal(value, reparsed[key]);
    }
}
