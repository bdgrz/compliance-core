using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Native identified close reconciliation at a verified immutable period cut; exact retries keep their original Unit body.</summary>
public sealed class RecordTypeIIPeriodCloseValidationHandler(TypeIIPeriodOperations operations,
    ITypeIIPeriodCloseReconciliation sources, IImmutableContentStore content)
    : IRequestHandler<RecordTypeIIPeriodCloseValidation, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<RecordTypeIIPeriodCloseValidation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Recording a period close requires a personal HTTP actor."));
        return await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PeriodId, request.CommandId,
            nameof(RecordTypeIIPeriodCloseValidation), request.ExpectedRevision, async (snapshot, operation, token) =>
            {
                var current = snapshot.View!;
                if (current.Status is not ("active" or "close_blocked" or "close_ready") ||
                    DateOnly.FromDateTime(operation.AcceptedAt.UtcDateTime) <= current.PeriodEnd)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The complete observation interval must end before its eligible close can be reconciled."));
                var read = await sources.ReadAsync(snapshot, context.Actor, user, operation, token).ConfigureAwait(false);
                if (!read.IsSuccess)
                    return Result.Failure(read.Error!);
                var reconciliation = read.Value;
                if (reconciliation.RecordedThrough != operation.AcceptedAt || reconciliation.SourceContentManifest is null)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The exact close receipt cutoff and published source proof are required."));
                ImmutableContentRef? change;
                var proof = await SnapshotSourcePublicationReader.ReadAsync(content, request.TenantId, request.ProgramId,
                    current.ExaminationId, reconciliation.SourceContentManifest, token).ConfigureAwait(false);
                if (reconciliation.SourceBlockers.Count == 0 && !TypeIBaselineSourceFamilies.Required.All(family =>
                        proof.SourceCoverage?.Count(row => row.Family == family) == 1))
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A ready close requires complete sealed source-family coverage, including every empty scan."));
                if (proof.RecordedThrough != operation.AcceptedAt || proof.ClosePeriodId != request.PeriodId ||
                    proof.CloseOperationId != request.CommandId || reconciliation.SourceReferences.Any(reference =>
                        !proof.Sources.Any(source => source.SourceType == reference.SourceType && source.RecordId == reference.RecordId &&
                            source.VersionId == reference.VersionId && source.Version == reference.Version && source.LogicalSha256 == reference.ContentSha256)))
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The complete close publication differs from its exact first owner, operation, cutoff or selected source versions."));
                if (snapshot.Rules.PrepareCloseValidation(request.ExpectedRevision, reconciliation, member, display, operation.AcceptedAt, out _) is { } failure)
                    return Result.Failure(failure.ToRequestError());
                change = await content.WriteJsonAsync(snapshot.Owner.Stream.Realm, Uuid.CreateVersion4(), TypeIIPeriodCloseValidationChange.Schema,
                    new TypeIIPeriodCloseValidationChange(new DurableIntentHeader(operation.RootOperationId, snapshot.Owner.Stream,
                        snapshot.Owner.Stream, snapshot.Owner.Stream, operation.Actor, operation.Episode, operation.OriginalExpectedRevision,
                        operation.AuthoredIntentSha256, operation.AcceptedAt), reconciliation), token).ConfigureAwait(false);
                return TypeIIPeriodOperations.Outcome(snapshot.Rules.RecordCloseValidation(operation, reconciliation, change!, null, null));
            }, ct).ConfigureAwait(false);
    }
}
