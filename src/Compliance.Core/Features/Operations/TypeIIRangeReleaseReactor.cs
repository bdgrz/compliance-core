using System.Globalization;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Settles the calendar from a period fact that ended a range: a canceled amendment or rejected opening releases
///     only its pending reservation (or fences it if it was never granted), and a frozen close releases the exact
///     live reservation named by the close fact. The period fact is committed first, so a delayed apply always meets
///     the fence before any release. Each settlement is a deterministic effect of its source fact, so a restarted
///     reactor replays the same command. An unreadable period or intent is a transient failure, never a release.
/// </summary>
sealed partial class TypeIIRangeReleaseReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IAggregateReader reader, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TypeIIPeriod.Area), WorkloadName),
        IReactorHandler<TypeIIPeriodAmendmentCanceled>, IReactorHandler<TypeIIPeriodOpeningRejected>,
        IReactorHandler<ObservationPeriodCloseFrozen>
{
    public const string WorkloadName = "TypeIIRangeReleaseV1";

    public ValueTask HandleAsync(IReactorContext<TypeIIPeriodAmendmentCanceled> context, CancellationToken ct) =>
        SettleAsync(context, context.Trigger.Operation.RootOperationId, context.Trigger.Intent,
            context.Trigger.Operation.Episode, ct);

    public ValueTask HandleAsync(IReactorContext<TypeIIPeriodOpeningRejected> context, CancellationToken ct) =>
        SettleAsync(context, context.Trigger.Operation.RootOperationId, context.Trigger.Intent,
            context.Trigger.Operation.Episode, ct);

    public async ValueTask HandleAsync(IReactorContext<ObservationPeriodCloseFrozen> context, CancellationToken ct)
    {
        var frozen = context.Trigger;
        var period = await reader.HydrateAsync(new TypeIIPeriod(frozen.TenantId, frozen.State.ProgramId,
            frozen.SubjectId), ct).ConfigureAwait(false);
        var intent = TypeIIPeriod.IntentOf(period.AcceptedFor(frozen.CalendarAuthorizationId));
        if (intent is null)
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "The operation that holds the closed range is not visible on the period yet.", true));
        await SettleAsync(context, frozen.CalendarAuthorizationId, intent, "initial", ct).ConfigureAwait(false);
    }

    async ValueTask SettleAsync(IReactorContext context, Uuid operationId, ImmutableContentRef intentReference,
        string episode, CancellationToken ct)
    {
        var realm = context.Source.Stream.Realm;
        TypeIIRangeIntent intent;
        try
        {
            intent = await TypeIIRangeContent.ReadIntentAsync(content, realm, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen range intent cannot be verified: {exception.Message}", true));
        }

        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var source = NativeFactReference.Of(context.Source.Stream, context.Source.Event);
        var settled = await bus.SendAsync(new SettleTypeIIPeriodRange(TypeIIRangeEffects.ReceiverCommandId(
            source.EventId, nameof(SettleTypeIIPeriodRange), episode), source,
            new TypeIIObservationCalendar(tenant, intent.ProgramId).Stream, operationId,
            TypeIIRangeEffects.Generation, null, intentReference, intent.PeriodId), context, ct)
            .ConfigureAwait(false);
        if (!settled.IsSuccess)
            throw new ReactionCommandFailedException(settled.Error!);
    }
}
