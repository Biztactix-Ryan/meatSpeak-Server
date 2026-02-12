using Xunit;
using MeatSpeak.Server.Capabilities;

namespace MeatSpeak.Server.Tests.Capabilities;

public class MsgIdGeneratorTests
{
    [Fact]
    public void Generate_ReturnsNonEmptyString()
    {
        var id = MsgIdGenerator.Generate();
        Assert.False(string.IsNullOrEmpty(id));
    }

    [Fact]
    public void Generate_ReturnsUniqueValues()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
            ids.Add(MsgIdGenerator.Generate());
        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void Generate_Returns26CharCrockfordBase32String()
    {
        var id = MsgIdGenerator.Generate();
        Assert.Equal(26, id.Length);
        Assert.True(id.All(c => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(c)));
    }

    [Fact]
    public void Generate_IsLexicographicallySortable()
    {
        var first = MsgIdGenerator.Generate();
        Thread.Sleep(2);
        var second = MsgIdGenerator.Generate();
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }
}
