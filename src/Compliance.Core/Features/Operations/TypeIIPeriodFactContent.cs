using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Replays the exact admitted immutable action, then compares its complete result with the native body reference.</summary>
static class TypeIIPeriodFactContent
{
    public static async ValueTask ApplyAsync(IImmutableContentStore content, TypeIIPeriodRules rules,
        DomainEvent value, TypeIIPeriodView after, CancellationToken ct)
    {
        var fact = (ITypeIIPeriodStateFact)value;
        var operation = fact.Operation;
        var realm = operation.Owner.Realm;
        CommandFailure? failure;
        switch (value)
        {
            case ObservationPeriodStarted started:
                {
                    var intent = await RangeAsync(content, started.Change, operation, rules.Id, ct).ConfigureAwait(false);
                    if (intent.OperationKind != TypeIIRangeOperationKind.Opening)
                        throw Invalid();
                    var opening = await content.ReadJsonAsync<TypeIIPeriodOpeningChange>(realm, intent.OpeningOrAmendmentContent, ct).ConfigureAwait(false);
                    var result = rules.ApplyOpening(operation, opening.State,
                        new TypeIIPeriodCalendarAuthorization(started.CalendarAuthorizationId, started.CalendarGeneration), started.OperationResult!);
                    if (!result.IsSuccess)
                        throw Invalid();
                    break;
                }
            case ObservationPeriodAmended amended:
                {
                    var intent = await RangeAsync(content, amended.Change, operation, rules.Id, ct).ConfigureAwait(false);
                    if (intent.OperationKind != TypeIIRangeOperationKind.Amendment)
                        throw Invalid();
                    var amendment = await content.ReadJsonAsync<TypeIIPeriodAmendmentChange>(realm, intent.OpeningOrAmendmentContent, ct).ConfigureAwait(false);
                    var result = rules.ApplyAmendment(operation, amendment,
                        new TypeIIPeriodCalendarAuthorization(amended.CalendarAuthorizationId, amended.CalendarGeneration), amended.OperationResult!);
                    if (!result.IsSuccess)
                        throw Invalid();
                    break;
                }
            case ObservationPeriodCloseProposed validation:
                {
                    if (validation.Change.Schema != TypeIIPeriodCloseValidationChange.Schema)
                        throw Invalid();
                    var mutation = await content.ReadJsonAsync<TypeIIPeriodCloseValidationChange>(realm, validation.Change, ct).ConfigureAwait(false);
                    if (mutation.Header != Header(operation))
                        throw Invalid();
                    failure = rules.RecordCloseValidation(operation, mutation.Content, validation.Change,
                        validation.FirstResponse, validation.OperationResult);
                    if (failure is not null)
                        throw Invalid();
                    break;
                }
            case TypeIIPeriodCloseApproved approved:
                {
                    await MutationAsync(content, rules, approved.Change, fact, ct).ConfigureAwait(false);
                    var approval = after.CloseApprovals[^1];
                    failure = rules.ApproveClose(Expected(operation), approval.AcknowledgedMaterialItemIds, approval.Rationale,
                        approval.ApprovedByMemberId, approval.ApproverRole, approval.ApproverDisplay, operation.AcceptedAt);
                    if (failure is not null)
                        throw Invalid();
                    break;
                }
            case ObservationPeriodCloseFrozen frozen:
                {
                    await MutationAsync(content, rules, frozen.Change, fact, ct).ConfigureAwait(false);
                    var close = after.CloseSnapshots[^1];
                    failure = rules.FreezeClose(Expected(operation), Snapshot(close), close.ActorMemberId, close.ActorRole, operation.AcceptedAt);
                    if (failure is not null)
                        throw Invalid();
                    break;
                }
            case TypeIIPeriodCloseAmended amended:
                {
                    var mutation = await MutationAsync(content, rules, amended.Change, fact, ct).ConfigureAwait(false);
                    if (mutation.SourceProof is not { Schema: TypeIIPeriodCloseMaterialProof.Schema } reference)
                        throw Invalid();
                    var proof = await content.ReadJsonAsync<TypeIIPeriodCloseMaterialProof>(realm, reference, ct).ConfigureAwait(false);
                    var prior = rules.Current!.CloseSnapshots[^1];
                    var close = after.CloseSnapshots[^1];
                    if (proof.Header != Header(operation) || proof.PeriodId != rules.Id ||
                        proof.SupersededCloseSnapshotId != prior.CloseSnapshotId || proof.SupersededCloseSha256 != prior.ContentSha256 ||
                        proof.SourcePublication != close.SourceContentManifest || !proof.ChangedRecordIds.SequenceEqual(close.ChangedRecordIds) ||
                        !proof.AffectedExaminationMaterialIds.SequenceEqual(close.AffectedExaminationMaterialIds) ||
                        proof.SourceFacts.Any(source => source.Fact.Stream.Realm != realm) ||
                        !proof.AffectedExaminationMaterialIds.Contains(prior.CloseSnapshotId))
                        throw Invalid();
                    failure = rules.AmendClose(Expected(operation), Snapshot(close), close.AmendmentReason!, close.ChangedRecordIds,
                        close.AffectedExaminationMaterialIds, close.ActorMemberId, close.ActorRole, close.ActorDisplay,
                        operation.AcceptedAt, reference);
                    if (failure is not null)
                        throw Invalid();
                    break;
                }
            case DueControlOccurrencesGenerated generated:
                rules.Apply(generated);
                break;
            default:
                throw Invalid();
        }
        if (rules.Current is null || ContentDigest.OfJson(rules.Current) != fact.CurrentContent.Sha256)
            throw Invalid();
        rules.ClearPendingChanges();
    }

    static async Task<TypeIIRangeIntent> RangeAsync(IImmutableContentStore content, ImmutableContentRef reference,
        OperationProvenance operation, Uuid periodId, CancellationToken ct)
    {
        var intent = await TypeIIRangeContent.ReadIntentAsync(content, operation.Owner.Realm, reference, ct).ConfigureAwait(false);
        if (intent.PeriodId != periodId || intent.Header.RootOperationId != operation.RootOperationId ||
            intent.Header.FirstOwner != operation.Owner || intent.Header.AffectedOwner != operation.Owner ||
            intent.Header.ExpectedRevisionOwner != operation.Owner || intent.Header.OriginalActor != operation.Actor ||
            intent.Header.AuthoredIntentSha256 != operation.AuthoredIntentSha256 ||
            intent.Header.OriginalExpectedRevision != operation.OriginalExpectedRevision || intent.Header.Episode != operation.Episode)
            throw Invalid();
        return intent;
    }

    static async Task<TypeIIPeriodMutationContent> MutationAsync(IImmutableContentStore content, TypeIIPeriodRules rules,
        ImmutableContentRef reference, ITypeIIPeriodStateFact fact, CancellationToken ct)
    {
        if (reference.Schema != TypeIIPeriodMutationContent.Schema)
            throw Invalid();
        var mutation = await content.ReadJsonAsync<TypeIIPeriodMutationContent>(fact.Operation.Owner.Realm, reference, ct).ConfigureAwait(false);
        if (mutation.Header != Header(fact.Operation) || mutation.Kind != fact.Operation.CommandKind || mutation.After != fact.CurrentContent ||
            mutation.BeforeSha256 != (rules.Current is null ? null : ContentDigest.OfJson(rules.Current)))
            throw Invalid();
        return mutation;
    }

    static DurableIntentHeader Header(OperationProvenance operation) => new(operation.RootOperationId, operation.Owner,
        operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
        operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt);
    static TypeIIPeriodCloseSnapshotContent Snapshot(TypeIIPeriodCloseSnapshotView close) => new(close.CloseSnapshotId,
        close.SourceReferences, close.ExpectedOccurrenceIds, close.AcknowledgedMaterialItemIds)
    { SourceContentManifest = close.SourceContentManifest };
    static long Expected(OperationProvenance operation) => operation.OriginalExpectedRevision is { } expected && expected <= long.MaxValue
        ? (long)expected : throw Invalid();
    static ImmutableContentIntegrityException Invalid() => new("The period's immutable action differs from its exact native acceptance or resulting body.");
}
