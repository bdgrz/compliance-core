using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Closes only the matching open draft from its committed discard and original reason.</summary>
public sealed class CloseMethodologyTemplateDraftHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content) : IRequestHandler<CloseMethodologyTemplateDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CloseMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only a committed draft discard reaction may close its line."));
        if (request.TemplateId == Uuid.Empty || request.DraftId == Uuid.Empty || request.Source is null ||
            request.Source.EventId == Uuid.Empty || request.Source.Stream != new MethodologyTemplateDraft(request.TemplateId, request.DraftId).Stream ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(CloseMethodologyTemplateDraft)))
            return Invalid();
        var (_, discarded, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateDraftDiscarded>(sources, content,
            MethodologyTemplate.Realm, request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (discarded.TemplateId != request.TemplateId || discarded.DraftId != request.DraftId || discarded.DiscardedBy == Uuid.Empty)
            return Invalid();
        return await executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId),
            owner => AggregateOutcome.CommitOnSuccess(owner.CloseDraft(request.DraftId, request.Source.EventId,
                discarded.Reason)), context, ct).ConfigureAwait(false);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Closing requires the exact committed discard of the named template's separate draft."));
}
