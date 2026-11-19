using System.Globalization;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Reactors;

/// <summary>Maps retirement and late occurrence openings to source-proven cancellations.</summary>
public sealed partial class ControlRetirementOccurrenceReactor(IProjectionCheckpointStore checkpoints,
    IAggregateReader reader, IDomainEventReader events, IRequestBus bus,
    ISourceFactResolver sources, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(), WorkloadName),
        IReactorHandler<ControlRetired>, IReactorHandler<ControlOccurrenceOpened>
{
    public const string WorkloadName = "ControlRetirementOccurrencesV1";

    public async ValueTask<Result> HandleAsync(IReactorContext<ControlRetired> context, CancellationToken ct)
    {
        var retired = context.Trigger;
        if (retired.ChangeCount == 0)
            return Result.Success;
        var stream = new ControlDraft(retired.TenantId, retired.ControlId).Stream;
        if (context.Source.Stream != stream || retired.Operation.Owner != stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The retirement trigger has another owner."));
        var (proof, actual, _) = await SourceFactProofs.BuildAsync<ControlRetired>(sources, content, stream.Realm,
            NativeFactReference.Of(stream, retired), context.Source.ResourceOffset, ct).ConfigureAwait(false);
        retired = actual;
        var selected = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(retired.TenantId.ToString(),
            ControlOccurrence.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event is not ControlOccurrenceOpened opened || opened.TenantId != retired.TenantId ||
                opened.ProgramId != retired.ProgramId || opened.ControlId != retired.ControlId ||
                !selected.Add(opened.OccurrenceId))
                continue;
            var result = await SendAsync(proof, retired, opened.OccurrenceId, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result;
        }
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IReactorContext<ControlOccurrenceOpened> context, CancellationToken ct)
    {
        var opened = context.Trigger;
        var occurrence = new ControlOccurrence(opened.TenantId, opened.ProgramId, opened.ControlId, opened.OccurrenceId).Stream;
        if (context.Source.Stream != occurrence || opened.Operation.Owner != occurrence)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The opening trigger has another owner."));
        var (_, actual, _) = await SourceFactProofs.BuildAsync<ControlOccurrenceOpened>(sources, content, occurrence.Realm,
            NativeFactReference.Of(occurrence, opened), context.Source.ResourceOffset, ct).ConfigureAwait(false);
        opened = actual;
        var stream = new ControlDraft(opened.TenantId, opened.ControlId).Stream;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(stream.Realm, stream.Area,
            stream.Resource), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event is not ControlRetired retired || retired.ChangeCount == 0 ||
                retired.ProgramId != opened.ProgramId)
                continue;
            var (proof, _, _) = await SourceFactProofs.BuildAsync<ControlRetired>(sources, content, stream.Realm,
                NativeFactReference.Of(stream, retired), record.ResourceOffset, ct).ConfigureAwait(false);
            return await SendAsync(proof, retired, opened.OccurrenceId, context, ct).ConfigureAwait(false);
        }
        return Result.Success;
    }

    async ValueTask<Result> SendAsync(SourceFactProof<ControlRetired> proof, ControlRetired retired,
        Uuid occurrenceId, IReactorContext context, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new ControlOccurrence(retired.TenantId, retired.ProgramId,
            retired.ControlId, occurrenceId), ct).ConfigureAwait(false);
        if (owner.Opened?.BusinessWindow is not ControlCalendarWindow window || window.From < retired.EffectiveUntil ||
            owner.Latest is not ControlOccurrenceOpened && owner.Latest is not ControlOccurrenceReviewed { Outcome: "returned" })
            return Result.Success;
        var command = new CancelRetiredControlOccurrence(Uuid.CreateVersion5(proof.EventId,
            "cancel-occurrence:" + occurrenceId + ":" + owner.Revision.ToString(CultureInfo.InvariantCulture)),
            retired.TenantId, retired.ProgramId, retired.ControlId, occurrenceId, owner.Revision, proof);
        return await bus.SendReactionAsync(command, context, ct).ConfigureAwait(false);
    }
}
