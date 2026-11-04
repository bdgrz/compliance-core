using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Records one terminal answer under the version application's atomic entry invariant.</summary>
public sealed class RecordTemplateEntryOutcomeHandler(IAggregateExecutor executor) : IRequestHandler<RecordTemplateEntryOutcome>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordTemplateEntryOutcome> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only an entry reaction may record its application outcome.")));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateId == Uuid.Empty ||
            request.TemplateVersion <= 0 || request.ApplicationEventId == Uuid.Empty || request.Outcome is null ||
            request.CommandId != Uuid.CreateVersion5(request.ApplicationEventId, "template-entry-outcome:" + request.Outcome.EntryKey))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The entry outcome must identify its exact application, entry and stable reaction command.")));
        return executor.ExecuteAsync(new TemplateVersionApplication(request.TenantId, request.ProgramId,
                request.TemplateId, request.TemplateVersion), owner => AggregateOutcome.CommitOnSuccess(
                owner.IsOpened && owner.ApplicationEventId != request.ApplicationEventId
                    ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The outcome belongs to another application source."))
                    : owner.Record(request.Outcome)), context, ct);
    }
}
