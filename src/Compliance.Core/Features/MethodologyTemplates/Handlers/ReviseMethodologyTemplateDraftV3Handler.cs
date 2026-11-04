using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Freezes original firm methodology entries and records their personal operator author.</summary>
public sealed class ReviseMethodologyTemplateDraftV3Handler(IAggregateExecutor executor, IImmutableContentStore content)
    : IRequestHandler<ReviseMethodologyTemplateDraftV3>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseMethodologyTemplateDraftV3> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!MethodologyTemplateDraftRequestActor.TryGet(context, request, out var author))
            return MethodologyTemplateDraftRequestActor.Forbidden();
        if (!MethodologyTemplateDraftRequestActor.ValidTarget(request) || request.ExpectedRevision < 0 ||
            !MethodologyTemplateDraft.ValidEntries(request.Entries))
            return MethodologyTemplateDraftRequestActor.Invalid();
        try
        {
            var snapshot = await content.WriteJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
                Uuid.CreateVersion5(MethodologyTemplateDraft.Identity(request.TemplateId, request.DraftId),
                    $"draft-revision:{request.CommandId}"), MethodologyTemplateDraft.EntriesSchema, request.Entries, ct)
                .ConfigureAwait(false);
            return await executor.ExecuteAsync(new MethodologyTemplateDraft(request.TemplateId, request.DraftId),
                owner => AggregateOutcome.CommitOnSuccess(owner.Revise(request.ExpectedRevision, request.Entries,
                    snapshot, author)), context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentConflictException)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The same draft revision command cannot replace its original immutable entry content."));
        }
    }
}
