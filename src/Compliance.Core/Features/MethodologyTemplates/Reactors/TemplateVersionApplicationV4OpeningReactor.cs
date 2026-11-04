using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Opens the per-version outcome owner from its committed canonical entry plan.</summary>
public sealed partial class TemplateVersionApplicationV4OpeningReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TemplateApplication.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateAppliedV4>
{
    public const string WorkloadName = "TemplateVersionApplicationOpeningV4";

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateAppliedV4> context, CancellationToken ct)
    {
        var source = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != source.TenantId.ToString() || source.Metadata.IsAudit ||
            !ReferenceEquals(source, context.Source.Event) || context.Source.Stream !=
                new TemplateApplication(source.TenantId, source.ProgramId, source.TemplateId, true).Stream)
            throw new InvalidOperationException("Version opening requires its exact bound canonical application source.");
        return bus.SendReactionAsync(new OpenTemplateVersionApplicationV4(source.TenantId, source.ProgramId,
            source.TemplateId, source.TemplateVersion, NativeFactReference.Of(context.Source.Stream, source),
            context.Source.ResourceOffset, Uuid.CreateVersion5(source.Metadata.EventId, nameof(OpenTemplateVersionApplicationV4))), context, ct);
    }
}
