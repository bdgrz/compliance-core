using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Advances parent ordering only from the matching committed entry-owner completion.</summary>
public sealed class RecordTemplateApplicationCompletedHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content) : IRequestHandler<RecordTemplateApplicationCompleted>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordTemplateApplicationCompleted> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only version-completion reactions close template ordering."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateId == Uuid.Empty ||
            request.TemplateVersion <= 0 || request.Source is null || request.Source.EventId == Uuid.Empty ||
            request.Source.Stream != new TemplateVersionApplication(request.TenantId, request.ProgramId, request.TemplateId, request.TemplateVersion).Stream ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(RecordTemplateApplicationCompleted)))
            return Invalid();
        var (_, completed, _) = await SourceFactProofs.BuildAsync<TemplateVersionApplicationCompleted>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (completed.TenantId != request.TenantId || completed.ProgramId != request.ProgramId ||
            completed.TemplateId != request.TemplateId || completed.TemplateVersion != request.TemplateVersion ||
            completed.ApplicationEventId == Uuid.Empty || completed.ApplicationCommandId == Uuid.Empty)
            return Invalid();
        return await executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true),
            owner => AggregateOutcome.CommitOnSuccess(owner.CompleteVersion(completed.ApplicationCommandId,
                completed.TemplateVersion, completed.Counts, request.Source.EventId)), context, ct).ConfigureAwait(false);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Completion requires the exact committed completion of the matching version's entry outcomes."));
}
