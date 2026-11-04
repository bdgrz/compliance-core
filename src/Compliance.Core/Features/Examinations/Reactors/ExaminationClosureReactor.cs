using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Reactors;

/// <summary>Maps each approved committed outcome sign-off to the examination's idempotent close.</summary>
public sealed partial class ExaminationClosureReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("examination-signoffs"), WorkloadName),
        IReactorHandler<ExaminationSignedOff>
{
    public const string WorkloadName = "ExaminationClosureV1";
    public ValueTask<Result> HandleAsync(IReactorContext<ExaminationSignedOff> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (fact.State.Status != "approved")
            return ValueTask.FromResult(Result.Success);
        var stream = new EventStreamAddress(fact.TenantId.ToString(), "examination-signoffs", fact.SubjectId.ToString());
        if (context.Source.Stream != stream || context.Source.ResourceOffset != 0 ||
            fact.Metadata.AggregateId != fact.SubjectId || fact.Metadata.AggregateVersion != 1)
            throw new InvalidOperationException("An examination close reaction must come from its canonical sign-off stream.");
        return bus.SendReactionAsync(new CloseExamination(fact.TenantId, fact.State.ProgramId,
            fact.State.Content.ExaminationId, new ExaminationClosure(fact.SubjectId,
                fact.Metadata.EventId, fact.State.Content.AuditOutcomeId)), context, ct);
    }
}
