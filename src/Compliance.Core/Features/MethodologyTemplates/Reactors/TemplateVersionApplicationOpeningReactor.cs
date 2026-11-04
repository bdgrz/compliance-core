using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Opens one per-version outcome record from the application's committed entry plan.</summary>
public sealed partial class TemplateVersionApplicationOpeningReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TemplateApplication.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateApplied>
{
    public const string WorkloadName = "TemplateVersionApplicationOpeningV1";

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateApplied> context, CancellationToken ct)
    {
        var source = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != source.TenantId.ToString() ||
            context.Source.Stream != new TemplateApplication(source.TenantId, source.Header.ProgramId, source.SubjectId).Stream ||
            source.Metadata.IsAudit || !ReferenceEquals(source, context.Source.Event))
            throw new InvalidOperationException("Application opening requires its exact bound committed tenant source.");
        return bus.SendReactionAsync(new OpenTemplateVersionApplication(source.TenantId, source.Header.ProgramId,
            source.Header.TemplateId, source.Header.TemplateVersionNumber, NativeFactReference.Of(context.Source.Stream, source),
            context.Source.ResourceOffset, Uuid.CreateVersion5(source.Metadata.EventId, nameof(OpenTemplateVersionApplication))),
            context, ct);
    }
}
