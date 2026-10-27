using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Snapshots.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Native immutable close amendment over complete fresh source proofs and authoritative affected material discovery.</summary>
public sealed class AmendTypeIIPeriodCloseHandler(TypeIIPeriodOperations operations,
    ManagementExecutiveAuthority executiveAuthority, ITypeIIPeriodCloseReconciliation sources,
    TypeIIPeriodCloseMaterialReader material, IImmutableContentStore content)
    : IRequestHandler<AmendTypeIIPeriodClose, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<AmendTypeIIPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Amending a period close requires a personal HTTP actor."));
        if (request.CloseSnapshotId == Uuid.Empty || string.IsNullOrWhiteSpace(request.AmendmentReason) || request.AmendmentReason.Trim().Length > 2000)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation, "A close amendment requires a new immutable identity and bounded rationale."));
        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId, request.ProgramId, user, member, ct).ConfigureAwait(false);
        if (role is null)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "A close amendment requires current program-scoped CEO or CISO authority."));
        return await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PeriodId, request.CommandId,
            nameof(AmendTypeIIPeriodClose), request.ExpectedRevision, async (snapshot, operation, token) =>
            {
                var current = snapshot.View!;
                if (current.Status != "closed" || current.CloseSnapshots.Count == 0 ||
                    current.CloseSnapshots.Any(row => row.CloseSnapshotId == request.CloseSnapshotId))
                    return Conflict("A close amendment requires the exact current closed period and a new snapshot identity.");
                var prior = current.CloseSnapshots[^1];
                var read = await sources.ReadAsync(snapshot, context.Actor, user, operation, token).ConfigureAwait(false);
                if (!read.IsSuccess)
                    return Result.Failure(read.Error!);
                var reconciliation = read.Value;
                if (reconciliation.RecordedThrough != operation.AcceptedAt || reconciliation.SourceContentManifest is not { } publication ||
                    reconciliation.SourceBlockers.Count != 0 || reconciliation.DuplicateOccurrenceIds.Count != 0 ||
                    reconciliation.MissingWorkIds.Count != 0 || !SameIds(reconciliation.ExpectedOccurrenceIds, reconciliation.ActualOccurrenceIds) ||
                    !SameIds(reconciliation.ExpectedOccurrenceIds, prior.ExpectedOccurrenceIds) ||
                    !SameIds(reconciliation.UnresolvedMaterialItemIds, prior.AcknowledgedMaterialItemIds))
                    return Conflict("A close amendment requires complete sources and work, preserving the exact original expected population and acknowledged exception set.");
                var published = await SnapshotSourcePublicationReader.ReadAsync(content, request.TenantId, request.ProgramId,
                    current.ExaminationId, publication, token).ConfigureAwait(false);
                if (published.RecordedThrough != operation.AcceptedAt || published.ClosePeriodId != request.PeriodId ||
                    published.CloseOperationId != request.CommandId || !TypeIBaselineSourceFamilies.Required.All(family =>
                        published.SourceCoverage?.Count(row => row.Family == family) == 1) ||
                    reconciliation.SourceReferences.Any(reference => !published.Sources.Any(source => source.SourceType == reference.SourceType &&
                        source.RecordId == reference.RecordId && source.VersionId == reference.VersionId && source.Version == reference.Version &&
                        source.LogicalSha256 == reference.ContentSha256)))
                    return Conflict("The close amendment source publication differs from its exact operation, cutoff or complete selected source versions.");
                var proofReference = await material.FreezeAsync(current, prior, reconciliation, operation, token).ConfigureAwait(false);
                var proof = await content.ReadJsonAsync<TypeIIPeriodCloseMaterialProof>(request.TenantId.ToString(), proofReference, token).ConfigureAwait(false);
                var body = new TypeIIPeriodCloseSnapshotContent(request.CloseSnapshotId, reconciliation.SourceReferences,
                    prior.ExpectedOccurrenceIds, prior.AcknowledgedMaterialItemIds)
                { SourceContentManifest = publication };
                return TypeIIPeriodOperations.Outcome(snapshot.Rules.AmendClose(request.ExpectedRevision, body,
                    request.AmendmentReason, proof.ChangedRecordIds, proof.AffectedExaminationMaterialIds, member, role, display,
                    operation.AcceptedAt, proofReference));
            }, ct).ConfigureAwait(false);
    }
    static bool SameIds(IReadOnlyList<Uuid> left, IReadOnlyList<Uuid> right) => left.OrderBy(id => id.ToString(), StringComparer.Ordinal)
        .SequenceEqual(right.OrderBy(id => id.ToString(), StringComparer.Ordinal));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
