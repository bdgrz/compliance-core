using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Publishes an approved separate draft or closes a discarded one on its template line.</summary>
public sealed partial class MethodologyTemplateDraftConclusionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplateDraft.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateDraftApproved>, IReactorHandler<MethodologyTemplateDraftDiscarded>
{
    public const string WorkloadName = "MethodologyTemplateDraftConclusionV1";

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateDraftApproved> context, CancellationToken ct)
    {
        var approved = context.Trigger;
        RequireSource(context, approved.TemplateId, approved.DraftId, approved);
        return bus.SendReactionAsync(new PublishMethodologyTemplateVersion(approved.TemplateId, approved.DraftId,
            NativeFactReference.Of(context.Source.Stream, approved), context.Source.ResourceOffset,
            Uuid.CreateVersion5(approved.Metadata.EventId, nameof(PublishMethodologyTemplateVersion))), context, ct);
    }

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateDraftDiscarded> context, CancellationToken ct)
    {
        var discarded = context.Trigger;
        RequireSource(context, discarded.TemplateId, discarded.DraftId, discarded);
        return bus.SendReactionAsync(new CloseMethodologyTemplateDraft(discarded.TemplateId, discarded.DraftId,
            NativeFactReference.Of(context.Source.Stream, discarded), context.Source.ResourceOffset,
            Uuid.CreateVersion5(discarded.Metadata.EventId, nameof(CloseMethodologyTemplateDraft))), context, ct);
    }

    static void RequireSource(IReactorContext context, Uuid templateId, Uuid draftId, DomainEvent fact)
    {
        if (templateId == Uuid.Empty || draftId == Uuid.Empty ||
            context.Source.Stream != new MethodologyTemplateDraft(templateId, draftId).Stream ||
            fact.Metadata.IsAudit || !ReferenceEquals(fact, context.Source.Event))
            throw new InvalidOperationException("A line conclusion requires the exact committed terminal fact of its separate draft.");
    }
}
