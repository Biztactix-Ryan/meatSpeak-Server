namespace MeatSpeak.Server.Core.Commands;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class FloodPenaltyAttribute : Attribute
{
    public int Cost { get; }

    public FloodPenaltyAttribute(int cost)
    {
        Cost = cost;
    }
}
