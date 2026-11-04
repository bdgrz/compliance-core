using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Releases parent version ordering after the exact child entry-owner completion.</summary>
public sealed partial class TemplateApplicationCompletionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TemplateVersionApplication.Area), WorkloadName),
        IReactorHandler<TemplateVersionApplicationCompleted>
{
    public const string WorkloadName = "TemplateApplicationCompletionV1";

    public ValueTask<Result> HandleAsync(IReactorContext<TemplateVersionApplicationCompleted> context, CancellationToken ct)
    {
        var source = context.Trigger;
        if (source.ApplicationCommandId == Uuid.Empty)
            return ValueTask.FromResult(Result.Success);
        if (Pattern.IsTenantTemplate || Pattern.Realm != source.TenantId.ToString() || source.Metadata.IsAudit ||
            !ReferenceEquals(source, context.Source.Event) || context.Source.Stream != new TemplateVersionApplication(
                source.TenantId, source.ProgramId, source.TemplateId, source.TemplateVersion).Stream)
            throw new InvalidOperationException("Parent completion requires its exact bound version-owner source.");
        return bus.SendReactionAsync(new RecordTemplateApplicationCompleted(source.TenantId, source.ProgramId,
            source.TemplateId, source.TemplateVersion, NativeFactReference.Of(context.Source.Stream, source),
            context.Source.ResourceOffset, Uuid.CreateVersion5(source.Metadata.EventId, nameof(RecordTemplateApplicationCompleted))), context, ct);
    }
}
