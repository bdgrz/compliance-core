using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

/// <summary>Closes only from the exact committed sign-off; caller-supplied closure claims are insufficient.</summary>
public sealed class CloseExaminationHandler(IAggregateReader reader, IDomainEventReader events,
    IAggregateExecutor executor) : IRequestHandler<CloseExamination>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CloseExamination> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only a committed sign-off reaction closes examinations."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.ExaminationId == Uuid.Empty)
            return Invalid();
        var owner = await reader.HydrateAsync(new Examination(request.TenantId, request.ExaminationId), ct).ConfigureAwait(false);
        if (owner.Current?.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The examination was not found in this program."));
        if (request.Closure is not { SignOffId: var signOffId, SignOffEventId: var eventId, OutcomeId: var outcomeId } ||
            signOffId == Uuid.Empty || eventId == Uuid.Empty || outcomeId == Uuid.Empty)
            return Invalid();
        ExaminationSignedOff? signedOff = null;
        var stream = new EventStreamAddress(request.TenantId.ToString(), "examination-signoffs", signOffId.ToString());
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (signedOff is not null || record.Stream != stream || record.ResourceOffset != 0 ||
                record.Event is not ExaminationSignedOff fact || fact.Metadata.EventId != eventId ||
                fact.Metadata.AggregateId != signOffId || fact.Metadata.AggregateVersion != 1 ||
                fact.SubjectId != signOffId || fact.TenantId != request.TenantId || fact.State is not { } state ||
                state.ProgramId != request.ProgramId || state.Content.ExaminationId != request.ExaminationId ||
                state.Content.AuditOutcomeId != outcomeId || state.Status != "approved" ||
                fact.ActorMemberId is not { } actor || actor == Uuid.Empty)
                return Invalid();
            signedOff = fact;
        }
        if (signedOff is null)
            return Invalid();
        var source = signedOff;
        return await executor.ExecuteAsync(new Examination(request.TenantId, request.ExaminationId), aggregate =>
            CommandFailureRequestAdapter.ToOutcome(aggregate.Close(request.Closure,
                source.ActorMemberId!.Value, source.State.ApproverDisplay, source.OccurredAt)), context, ct).ConfigureAwait(false);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The closing sign-off must be the exact committed approved source for this examination and outcome."));
}
