using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Reactors;

/// <summary>Reports committed action completions and cancellations to the finding that admitted them.</summary>
public sealed partial class CorrectiveActionOutcomeReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    ISourceFactResolver sources, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(CorrectiveAction.Area), WorkloadName),
        IReactorHandler<CorrectiveActionCompletedV2>, IReactorHandler<CorrectiveActionCancelled>
{
    public const string WorkloadName = "CorrectiveActionOutcomeV1";

    public ValueTask<Result> HandleAsync(IReactorContext<CorrectiveActionCompletedV2> context, CancellationToken ct) =>
        ReportAsync(context, context.Trigger.TenantId, context.Trigger.ProgramId, context.Trigger.FindingId,
            context.Trigger.ActionId, "completed", ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CorrectiveActionCancelled> context, CancellationToken ct) =>
        ReportAsync(context, context.Trigger.TenantId, context.Trigger.ProgramId, context.Trigger.FindingId,
            context.Trigger.ActionId, "cancelled", ct);

    async ValueTask<Result> ReportAsync<TEvent>(IReactorContext<TEvent> context, Uuid tenant, Uuid program,
        Uuid finding, Uuid action, string outcome, CancellationToken ct) where TEvent : DomainEvent
    {
        var expected = new CorrectiveAction(tenant, program, finding, action).Stream;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context, expected, ct).ConfigureAwait(false);
        return await bus.SendReactionAsync(new RecordCorrectiveActionOutcome(
            Uuid.CreateVersion5(context.Trigger.Metadata.EventId, nameof(RecordCorrectiveActionOutcome)), tenant,
            program, finding, action, outcome, NativeFactReference.Of(expected, context.Trigger),
            context.Source.ResourceOffset), context, ct).ConfigureAwait(false);
    }
}
