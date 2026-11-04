using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Opens the separate draft after the template line commits its start.</summary>
public sealed partial class MethodologyTemplateDraftOpeningReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplate.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateDraftStarted>
{
    public const string WorkloadName = "MethodologyTemplateDraftOpeningV1";

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateDraftStarted> context, CancellationToken ct)
    {
        var started = context.Trigger;
        if (started.TemplateId == Uuid.Empty || started.DraftId == Uuid.Empty ||
            context.Source.Stream != new MethodologyTemplate(started.TemplateId).Stream ||
            started.Metadata.IsAudit || !ReferenceEquals(started, context.Source.Event))
            throw new InvalidOperationException("Draft opening requires the exact committed template line start.");
        return bus.SendReactionAsync(new OpenMethodologyTemplateDraft(started.TemplateId, started.DraftId,
            NativeFactReference.Of(context.Source.Stream, started), context.Source.ResourceOffset,
            Uuid.CreateVersion5(started.Metadata.EventId, nameof(OpenMethodologyTemplateDraft))), context, ct);
    }
}
