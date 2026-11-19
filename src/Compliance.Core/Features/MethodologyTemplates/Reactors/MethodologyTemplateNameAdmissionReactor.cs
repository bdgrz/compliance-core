using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Creates the admitted idle template line and waits for a definite creation or recorded refusal before checkpointing.</summary>
public sealed partial class MethodologyTemplateNameAdmissionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplateNameClaim.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateNameClaimed>
{
    public const string WorkloadName = "MethodologyTemplateNameAdmissionV1";

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateNameClaimed> context, CancellationToken ct)
    {
        var claim = context.Trigger;
        if (context.Source.Stream.Realm != MethodologyTemplate.Realm || context.Source.Stream.Area != MethodologyTemplateNameClaim.Area ||
            context.Source.Stream.Resource != claim.Metadata.AggregateId.ToString() || claim.Operation.Owner != context.Source.Stream ||
            claim.Owner.Realm != MethodologyTemplate.Realm || claim.Owner.Area != MethodologyTemplate.Area ||
            !Uuid.TryParse(claim.Owner.Resource, CultureInfo.InvariantCulture, out var templateId) || templateId == Uuid.Empty ||
            claim.Metadata.IsAudit || !ReferenceEquals(claim, context.Source.Event))
            throw new InvalidOperationException("Template creation requires the exact committed global name admission for its platform line.");
        return bus.SendReactionAsync(new CreateMethodologyTemplateLine(templateId,
            NativeFactReference.Of(context.Source.Stream, claim), context.Source.ResourceOffset,
            Uuid.CreateVersion5(claim.Metadata.EventId, nameof(CreateMethodologyTemplateLine))), context, ct);
    }
}
