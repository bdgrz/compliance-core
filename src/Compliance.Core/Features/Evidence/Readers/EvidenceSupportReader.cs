using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;

namespace Bdgrz.Compliance.Features.Evidence.Readers;

/// <summary>Separates new-use eligibility from the immutable assertion retained for historical reliance.</summary>
public sealed class EvidenceSupportReader(IAggregateReader reader)
{
    /// <summary>Reads the original assertion, including after withdrawal; this does not admit new reliance.</summary>
    public async ValueTask<Result<EvidenceSupportView>> ReadOriginalAsync(Uuid tenantId, Uuid programId,
        Uuid supportId, CancellationToken ct = default)
    {
        var owner = await ReadAsync(tenantId, programId, supportId, ct).ConfigureAwait(false);
        return owner is null ? Missing() : Result<EvidenceSupportView>.Success(owner.View()!);
    }

    /// <summary>Refuses a withdrawn assertion when selecting support for new use.</summary>
    public async ValueTask<Result<EvidenceSupportView>> RequireActiveAsync(Uuid tenantId, Uuid programId,
        Uuid supportId, CancellationToken ct = default)
    {
        var owner = await ReadAsync(tenantId, programId, supportId, ct).ConfigureAwait(false);
        if (owner is null)
            return Missing();
        return owner.IsActive
            ? Result<EvidenceSupportView>.Success(owner.View()!)
            : Result<EvidenceSupportView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Withdrawn evidence support cannot be selected for new reliance."));
    }

    /// <summary>Reads current lifecycle status without changing the immutable link receipt.</summary>
    public async ValueTask<Result<EvidenceSupportStatusView>> ReadStatusAsync(Uuid tenantId, Uuid programId,
        Uuid supportId, CancellationToken ct = default)
    {
        var owner = await ReadAsync(tenantId, programId, supportId, ct).ConfigureAwait(false);
        return owner is null
            ? Result<EvidenceSupportStatusView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence support link was not found in this program."))
            : Result<EvidenceSupportStatusView>.Success(Status(owner));
    }

    internal static EvidenceSupportStatusView Status(EvidenceSupport owner) => new(owner.View()!, owner.Revision,
        owner.IsActive, owner.Withdrawal is not { } withdrawn ? null : new(withdrawn.CommandId, withdrawn.Reason,
            withdrawn.ActorMemberId, withdrawn.ActorDisplay, withdrawn.OccurredAt));

    async ValueTask<EvidenceSupport?> ReadAsync(Uuid tenantId, Uuid programId, Uuid supportId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || supportId == Uuid.Empty)
            return null;
        var owner = await reader.HydrateAsync(EvidenceSupport.ForVerification(tenantId, supportId), ct).ConfigureAwait(false);
        return owner.Current?.ProgramId == programId ? owner : null;
    }

    static Result<EvidenceSupportView> Missing() => Result<EvidenceSupportView>.Failure(
        new RequestError(RequestErrorKind.NotFound, "The evidence support link was not found in this program."));
}
