using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Immutable period bodies and compact native fact construction over exact prepared rules.</summary>
public static class TypeIIPeriodContent
{
    public const string StateSchema = "type-ii-period-state.v1";

    public static TypeIIPeriodAuthorityHeader Header(TypeIIPeriodView view)
    {
        var validation = view.CloseValidation;
        var approval = view.CloseApprovals.Count == 0 ? null : view.CloseApprovals[^1];
        var close = view.CloseSnapshots.Count == 0 ? null : view.CloseSnapshots[^1];
        return new TypeIIPeriodAuthorityHeader(view.TenantId, view.ProgramId, view.PeriodId, view.Revision,
            view.Status, view.ExaminationId, view.TypeIIPlanId, view.TypeIIPlanVersion, view.TypeIIPlanContentSha256,
            view.PeriodStart, view.PeriodEnd, view.OpeningSnapshotId, view.OpeningSnapshotSha256,
            view.ExpectedOccurrences.Count, ContentDigest.OfJson(view.ExpectedOccurrences),
            validation is null ? null : ContentDigest.OfJson(validation), validation?.Status, validation?.ValidatorMemberId, validation?.ValidatedAt,
            approval is null ? null : ContentDigest.OfJson(approval), approval?.ApprovedByMemberId, approval?.ApproverRole, approval?.ApprovedAt,
            view.CloseSnapshots.Count, close?.CloseSnapshotId, close?.Version,
            close is null ? null : ContentDigest.OfJson(close), close?.ActorMemberId, close?.ActorRole,
            close?.OriginalExpectedRevision, view.ActorMemberId, view.UpdatedAt);
    }

    public static async ValueTask<FrozenTypeIIPeriodMutation> FreezeAsync(IImmutableContentStore store,
        TypeIIPeriodView? before, TypeIIPeriodRules rules, OperationProvenance operation, CancellationToken ct)
    {
        var after = rules.Current ?? throw new InvalidOperationException("A period mutation requires its complete verified result.");
        var mutations = rules.PendingChanges;
        if (mutations.Count != 1)
            throw new InvalidOperationException("One native period append requires exactly one prepared rule mutation.");
        var body = await store.WriteJsonAsync(operation.Owner.Realm, Uuid.CreateVersion4(), StateSchema, after, ct).ConfigureAwait(false);
        var rule = mutations[0];
        var change = rule switch
        {
            ObservationPeriodStartedBody started => started.Change,
            ObservationPeriodAmendedBody amended => amended.Change,
            ObservationPeriodCloseProposedBody validation => validation.Change,
            DueControlOccurrencesGenerated => null,
            _ => await store.WriteJsonAsync(operation.Owner.Realm, Uuid.CreateVersion4(), TypeIIPeriodMutationContent.Schema,
                new TypeIIPeriodMutationContent(new DurableIntentHeader(operation.RootOperationId, operation.Owner,
                    operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
                    operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt),
                    operation.CommandKind, before is null ? null : ContentDigest.OfJson(before), body,
                    rule is TypeIIPeriodCloseAmendedBody amended ? amended.SourceProof : null), ct).ConfigureAwait(false),
        };
        return new FrozenTypeIIPeriodMutation(before, after, rule, body, change);
    }

    public static DomainEvent Fact(FrozenTypeIIPeriodMutation prepared, OperationProvenance operation,
        ImmutableContentRef? firstResponse, ImmutableContentRef operationResult, long sequence,
        ImmutableContentRef? calendarIntent = null)
    {
        var state = prepared.After;
        var header = Header(state);
        var current = prepared.CurrentContent;
        var change = prepared.Change;
        return prepared.RuleMutation switch
        {
            ObservationPeriodStartedBody started => new ObservationPeriodStarted(state.TenantId, state.PeriodId,
                state.Revision, started.ActorMemberId, change!, started.OccurredAt, operation, started.CalendarAuthorizationId,
                started.CalendarGeneration, firstResponse, operationResult, sequence, header, current),
            ObservationPeriodAmendedBody amended => new ObservationPeriodAmended(state.TenantId, state.PeriodId,
                state.Revision, amended.ActorMemberId, change!, amended.OccurredAt, operation, amended.CalendarAuthorizationId,
                amended.CalendarGeneration, firstResponse, operationResult, sequence, header, current),
            ObservationPeriodCloseProposedBody validation => new ObservationPeriodCloseProposed(state.TenantId, state.PeriodId,
                state.Revision, validation.ActorMemberId, change!, validation.OccurredAt, operation, firstResponse,
                operationResult, sequence, header, current),
            TypeIIPeriodCloseApprovedBody approved => new TypeIIPeriodCloseApproved(state.TenantId, state.PeriodId,
                state.Revision, approved.ActorMemberId, change!, approved.OccurredAt, operation, firstResponse,
                operationResult, sequence, header, current),
            ObservationPeriodCloseFrozenBody frozen => new ObservationPeriodCloseFrozen(state.TenantId, state.PeriodId,
                state.Revision, frozen.ActorMemberId, change!, frozen.OccurredAt, operation, frozen.CalendarAuthorizationId,
                frozen.CalendarGeneration, firstResponse, operationResult, sequence, header, current,
                calendarIntent ?? throw new InvalidOperationException("A close freeze requires the exact admitted calendar intent.")),
            TypeIIPeriodCloseAmendedBody amended => new TypeIIPeriodCloseAmended(state.TenantId, state.PeriodId,
                state.Revision, amended.ActorMemberId, change!, amended.OccurredAt, operation, firstResponse,
                operationResult, sequence, header, current),
            DueControlOccurrencesGenerated generated => generated with
            {
                NativeSequence = sequence,
                Header = header,
                CurrentContent = current,
                FirstResponse = firstResponse,
                OperationResult = operationResult,
            },
            _ => throw new InvalidOperationException("The period mutation has another native fact kind."),
        };
    }

    public static string Transition(DomainEvent value) => value switch
    {
        ObservationPeriodStarted => "start",
        ObservationPeriodAmended => "period_amendment",
        DueControlOccurrencesGenerated => "generation",
        ObservationPeriodCloseProposed => "validation",
        TypeIIPeriodCloseApproved => "approval",
        ObservationPeriodCloseFrozen => "freeze",
        TypeIIPeriodCloseAmended => "close_amendment",
        _ => throw new InvalidOperationException("The period mutation has another transition kind."),
    };

    public static bool SameGenerationState(TypeIIPeriodView before, TypeIIPeriodView after) =>
        after.Revision == before.Revision + 1 && ContentDigest.OfJson(before with { Revision = after.Revision }) == ContentDigest.OfJson(after);

}
