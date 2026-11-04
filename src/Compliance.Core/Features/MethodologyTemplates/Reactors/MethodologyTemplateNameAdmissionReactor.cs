using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

public sealed partial class MethodologyTemplateNameAdmissionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplateNameClaim.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateNameClaimed>
{
    public const string WorkloadName = "MethodologyTemplateNameAdmissionV1";
    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateNameClaimed> context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Name admission {context.Trigger.Metadata.EventId} creation reaction is not implemented.")));
    }
}
