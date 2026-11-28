namespace Supermarket.BuildingBlocks.Identity;

/// <summary>Describes the authenticated actor executing the current application operation.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }
    Guid? EmployeeId { get; }
    Guid? BranchId { get; }
    string? DeviceId { get; }
    bool IsAuthenticated { get; }
}