using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Services;

/// <summary>Business authority uses current source staff and independently retained duty state.</summary>
sealed class FirmServicesAuthority(IAggregateReader reader, FirmStaffViews staff,
    IPlatformOperatorAccess operators)
{
    public async ValueTask<Result<Uuid>> RequireOperatorAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return Failure(RequestErrorKind.Unauthorized, "An authenticated platform operator is required.");
        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? Result<Uuid>.Success(userId)
            : Failure(RequestErrorKind.Forbidden, "The actor is not a current platform operator.");
    }

    public async ValueTask<Result<Uuid>> RequirePartnerAsync(ClaimsPrincipal actor, string duty, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return Failure(RequestErrorKind.Unauthorized, "An authenticated partner is required.");
        if (!await IsActiveStaffAsync(userId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden, "Partner authority requires current active firm staff.");
        var owner = await reader.HydrateAsync(new PartnerDesignation(userId, duty), ct).ConfigureAwait(false);
        return owner.IsDesignated
            ? Result<Uuid>.Success(userId)
            : Failure(RequestErrorKind.Forbidden, "The actor does not currently hold the required partner duty.");
    }

    public async ValueTask<bool> IsActiveStaffAsync(Uuid userId, CancellationToken ct) =>
        userId != Uuid.Empty && await staff.FindByUserAsync(userId, ct).ConfigureAwait(false) is { IsActive: true };

    static Result<Uuid> Failure(RequestErrorKind kind, string message) => Result<Uuid>.Failure(new(kind, message));
}
