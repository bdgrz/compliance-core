using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Reactors;

/// <summary>
///     Settles the calendar from a period fact that ended a range: a canceled amendment or rejected opening releases
///     only its pending reservation (or fences it if it was never granted), and a frozen close releases the exact
///     live reservation named by the close fact. The period fact is committed first, so a delayed apply always meets
///     the fence before any release. Each settlement is a deterministic effect of its source fact, so a restarted
///     reactor replays the same command. An unreadable period or intent is a transient failure, never a release.
/// </summary>
sealed partial class TypeIIRangeReleaseReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IImmutableContentStore content)
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

    public ValueTask HandleAsync(IReactorContext<ObservationPeriodCloseFrozen> context, CancellationToken ct) =>
        SettleAsync(context, context.Trigger.CalendarAuthorizationId, context.Trigger.CalendarIntent, "initial", ct);

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
                "The frozen range intent cannot be verified.", exception is ImmutableContentUnavailableException));
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
