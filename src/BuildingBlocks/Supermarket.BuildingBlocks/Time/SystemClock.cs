namespace Supermarket.BuildingBlocks.Time;

/// <summary>Production clock backed by the system UTC clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}