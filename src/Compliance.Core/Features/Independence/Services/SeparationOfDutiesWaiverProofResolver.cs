using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.Independence.Services;

/// <summary>
/// Resolves waiver proofs by hydrating the waiver aggregate and re-reading its approval event through the source
/// fact resolver. The waiver stream holds the recorded event at position 0 and, once approved, the approval at
/// position 1; no other event exists, so the approval locator is never searched for.
/// </summary>
public sealed class SeparationOfDutiesWaiverProofResolver(IAggregateReader reader, ISourceFactResolver facts)
    : ISeparationOfDutiesWaiverProofResolver
{
    const ulong ApprovalPosition = 1;

    /// <inheritdoc />
    public async ValueTask<Result<SeparationOfDutiesWaiverProof>> ResolveAsync(
        SeparationOfDutiesWaiverProofScope scope, DateTimeOffset at, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!IsWellFormed(scope))
            return Result<SeparationOfDutiesWaiverProof>.Failure(Invalid());
        var proof = await BuildAsync(scope, ct).ConfigureAwait(false);
        if (proof is null || !Authorizes(proof, scope, at))
            return Result<SeparationOfDutiesWaiverProof>.Failure(Denied());
        return Result<SeparationOfDutiesWaiverProof>.Success(proof);
    }

    /// <inheritdoc />
    public async ValueTask<Result> VerifyAsync(SeparationOfDutiesWaiverProof proof, DateTimeOffset at,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proof);
        var scope = proof.ToScope();
        if (!IsWellFormed(scope))
            return Result.Failure(Invalid());
        var stored = await BuildAsync(scope, ct).ConfigureAwait(false);
        if (stored is null || !Authorizes(stored, scope, at) ||
            !StringComparer.Ordinal.Equals(ContentDigest.OfJson(stored), ContentDigest.OfJson(proof)))
            return Result.Failure(Denied());
        return Result.Success;
    }

    static bool Authorizes(SeparationOfDutiesWaiverProof proof, SeparationOfDutiesWaiverProofScope scope,
        DateTimeOffset at) => proof.ToScope() == scope && at >= proof.ValidFrom && at < proof.ValidUntil;

    async ValueTask<SeparationOfDutiesWaiverProof?> BuildAsync(SeparationOfDutiesWaiverProofScope scope,
        CancellationToken ct)
    {
        var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(scope.TenantId, scope.WaiverId), ct)
            .ConfigureAwait(false);
        if (!waiver.IsRecorded || waiver.ApprovedAt is not { } approvedAt ||
            waiver.ApproverMemberId is not { } approver || waiver.Scope is not { } recorded)
            return null;
        ResolvedSourceFact fact;
        try
        {
            fact = await facts.ResolveAsync(waiver.Stream, ApprovalPosition, ct).ConfigureAwait(false);
        }
        catch (SourceFactVerificationException)
        {
            return null;
        }

        if (fact.Event is not SeparationOfDutiesWaiverApproved approval ||
            EventKind.Of(approval) != EventKind.Of(typeof(SeparationOfDutiesWaiverApproved)) ||
            approval.TenantId != scope.TenantId || approval.WaiverId != scope.WaiverId ||
            approval.ApproverMemberId != approver || approval.ApprovedAt != approvedAt ||
            fact.Locator.Source != waiver.Stream)
            return null;
        return new SeparationOfDutiesWaiverProof(waiver.TenantId, waiver.Id, waiver.BeneficiaryMemberId,
            waiver.BeneficiaryMemberId, recorded.Action, recorded.RecordType, recorded.RecordId,
            recorded.VersionId, recorded.Revision, fact.Locator, approvedAt, waiver.ExpiresAt);
    }

    static bool IsWellFormed(SeparationOfDutiesWaiverProofScope scope) =>
        scope.TenantId != Uuid.Empty && scope.WaiverId != Uuid.Empty && scope.SubjectId != Uuid.Empty &&
        scope.ActorId != Uuid.Empty && scope.TargetId != Uuid.Empty && scope.TargetVersionId != Uuid.Empty &&
        scope.TargetRevision > 0 && !string.IsNullOrWhiteSpace(scope.Action) &&
        !string.IsNullOrWhiteSpace(scope.TargetType);

    static RequestError Invalid() => new(RequestErrorKind.Validation,
        "A separation-of-duties waiver proof requires a complete exact scope.");

    static RequestError Denied() => new(RequestErrorKind.Forbidden,
        "No approved separation-of-duties waiver authorizes this exact scope.");
}
