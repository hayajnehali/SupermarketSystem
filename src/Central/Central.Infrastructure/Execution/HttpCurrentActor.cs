using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Supermarket.BuildingBlocks.Identity;

namespace Central.Infrastructure.Execution;

/// <summary>
/// Reads the authenticated actor from the current HTTP principal.
/// Background operations must provide an explicit system actor when they write audited data.
/// </summary>
public sealed class HttpCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
    private bool IsPrincipalAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => IsPrincipalAuthenticated
        ? ReadGuidClaim(Principal!, ClaimTypes.NameIdentifier, "sub")
        : null;

    public Guid? EmployeeId => IsPrincipalAuthenticated
        ? ReadGuidClaim(Principal!, "employee_id")
        : null;

    public Guid? BranchId => IsPrincipalAuthenticated
        ? ReadGuidClaim(Principal!, "branch_id")
        : null;

    public string? DeviceId => IsPrincipalAuthenticated
        ? Principal!.FindFirst("device_id")?.Value
        : null;

    public bool IsAuthenticated => IsPrincipalAuthenticated;

    private static Guid? ReadGuidClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (Guid.TryParse(value, out var id))
                return id;
        }

        return null;
    }
}
