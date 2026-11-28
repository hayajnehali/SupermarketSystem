namespace Supermarket.BuildingBlocks.Time;

/// <summary>Provides UTC time without coupling application logic to the system clock.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}