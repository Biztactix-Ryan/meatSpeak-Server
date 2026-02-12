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
    public void Generate_Returns32CharHexString()
    {
        var id = MsgIdGenerator.Generate();
        Assert.Equal(32, id.Length);
        Assert.True(id.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
    }
}
