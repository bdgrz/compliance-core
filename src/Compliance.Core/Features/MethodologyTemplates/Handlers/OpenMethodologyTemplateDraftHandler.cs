using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Opens a separate draft only from its exact immutable line-start source.</summary>
public sealed class OpenMethodologyTemplateDraftHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content) : IRequestHandler<OpenMethodologyTemplateDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<OpenMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only a line-start reaction may open its draft."));
        if (request.TemplateId == Uuid.Empty || request.DraftId == Uuid.Empty || request.Source is null ||
            request.Source.EventId == Uuid.Empty || request.Source.Stream != new MethodologyTemplate(request.TemplateId).Stream ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(OpenMethodologyTemplateDraft)))
            return Invalid();
        var (_, started, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateDraftStarted>(sources, content,
            MethodologyTemplate.Realm, request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (started.TemplateId != request.TemplateId || started.DraftId != request.DraftId || started.StartedBy == Uuid.Empty ||
            started.EntriesContent is null || started.EntriesContent.Schema != MethodologyTemplateDraft.EntriesSchema)
            return Invalid();
        var entries = await content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
            started.EntriesContent, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new MethodologyTemplateDraft(request.TemplateId, request.DraftId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Open(request.Source.EventId, started.BasedOnVersion,
                entries, started.EntriesContent)), context, ct).ConfigureAwait(false);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "A draft must open from its exact committed template line start and original entry snapshot."));
}
