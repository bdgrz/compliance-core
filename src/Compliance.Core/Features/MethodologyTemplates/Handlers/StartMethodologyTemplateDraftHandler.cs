using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Starts the sole separate draft using the authoritative line's latest immutable entries.</summary>
public sealed class StartMethodologyTemplateDraftHandler(IAggregateExecutor executor, IAggregateReader reader,
    IImmutableContentStore content) : IRequestHandler<StartMethodologyTemplateDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<StartMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!MethodologyTemplateDraftRequestActor.TryGet(context, request, out var startedBy))
            return MethodologyTemplateDraftRequestActor.Forbidden();
        if (!MethodologyTemplateDraftRequestActor.ValidTarget(request))
            return MethodologyTemplateDraftRequestActor.Invalid();
        var line = await reader.HydrateAsync(new MethodologyTemplate(request.TemplateId), ct).ConfigureAwait(false);
        if (!line.IsLineCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The separate-draft template line was not found."));
        var prior = line.DraftStartFor(request.DraftId);
        if (prior is null && (line.IsRetired || line.OpenDraftId is not null))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Only an active idle template line starts a draft."));
        var snapshot = prior?.EntriesContent ?? line.PublishedEntriesContent;
        IReadOnlyList<MethodologyTemplateRecordContent> entries;
        if (snapshot is null)
        {
            entries = [];
            snapshot = await content.WriteJsonAsync(MethodologyTemplate.Realm,
                Uuid.CreateVersion5(MethodologyTemplateDraft.Identity(request.TemplateId, request.DraftId), "opening-entries"),
                MethodologyTemplateDraft.EntriesSchema, entries, ct).ConfigureAwait(false);
        }
        else
            entries = await content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
                snapshot, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId),
            owner => AggregateOutcome.CommitOnSuccess(owner.StartDraft(request.DraftId,
                prior?.BasedOnVersion ?? line.PublishedVersionNumber, entries, snapshot, startedBy)), context, ct).ConfigureAwait(false);
    }
}
