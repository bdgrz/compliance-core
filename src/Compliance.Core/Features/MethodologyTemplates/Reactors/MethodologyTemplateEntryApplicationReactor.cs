using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Maps the committed immutable plan to one stable command per changed or new entry.</summary>
public sealed partial class MethodologyTemplateEntryApplicationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TemplateApplication.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateAppliedV4>
{
    public const string WorkloadName = "MethodologyTemplateEntryApplicationV1";

    public async ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateAppliedV4> context, CancellationToken ct)
    {
        var source = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != source.TenantId.ToString() || source.Metadata.IsAudit ||
            context.Source.Stream != new TemplateApplication(source.TenantId, source.ProgramId, source.TemplateId, true).Stream ||
            !ReferenceEquals(source, context.Source.Event))
            throw new InvalidOperationException("Entry application requires its exact committed canonical application source.");
        foreach (var key in source.EntriesToCreate.Concat(source.EntriesToPropose))
        {
            var result = await bus.SendReactionAsync(new ApplyMethodologyTemplateEntry(source.TenantId, source.ProgramId,
                source.TemplateId, source.TemplateVersion, key, NativeFactReference.Of(context.Source.Stream, source),
                context.Source.ResourceOffset, Uuid.CreateVersion5(source.Metadata.EventId, "apply-template-entry:" + key)),
                context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result;
        }
        return Result.Success;
    }
}
