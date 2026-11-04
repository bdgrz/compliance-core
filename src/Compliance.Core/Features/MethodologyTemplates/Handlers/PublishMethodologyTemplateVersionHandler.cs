using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Publishes only the exact immutable approval committed by the line's open separate draft.</summary>
public sealed class PublishMethodologyTemplateVersionHandler(IAggregateExecutor executor, IAggregateReader reader,
    ISourceFactResolver sources, IImmutableContentStore content) : IRequestHandler<PublishMethodologyTemplateVersion>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<PublishMethodologyTemplateVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only a committed draft approval reaction may publish a version."));
        if (request.TemplateId == Uuid.Empty || request.DraftId == Uuid.Empty || request.Source is null ||
            request.Source.EventId == Uuid.Empty || request.Source.Stream != new MethodologyTemplateDraft(request.TemplateId, request.DraftId).Stream ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(PublishMethodologyTemplateVersion)))
            return Invalid();
        var (_, approved, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateDraftApproved>(sources, content,
            MethodologyTemplate.Realm, request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (approved.TemplateId != request.TemplateId || approved.DraftId != request.DraftId || approved.ApprovedBy == Uuid.Empty ||
            !MethodologyTemplateDraft.ValidReference(approved.EntriesContent))
            return Invalid();
        var entries = await content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
            approved.EntriesContent, ct).ConfigureAwait(false);
        var line = await reader.HydrateAsync(new MethodologyTemplate(request.TemplateId), ct).ConfigureAwait(false);
        var prior = line.PublicationFor(request.DraftId);
        IReadOnlyList<string> changed;
        if (prior is not null)
            changed = prior.ChangedEntryKeys;
        else
        {
            IReadOnlyList<MethodologyTemplateRecordContent> previous = line.PublishedEntriesContent is { } previousContent
                ? await content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
                    previousContent, ct).ConfigureAwait(false) : [];
            changed = ChangedKeys(previous, entries);
        }
        return await executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId),
            owner => AggregateOutcome.CommitOnSuccess(owner.PublishDraft(request.DraftId, request.Source.EventId,
                approved.Revision, line.PublishedVersionNumber, entries, approved.EntriesContent, changed, approved.ApprovedBy)), context, ct)
            .ConfigureAwait(false);
    }

    static string[] ChangedKeys(IReadOnlyList<MethodologyTemplateRecordContent> previous,
        IReadOnlyList<MethodologyTemplateRecordContent> entries)
    {
        var before = previous.ToDictionary(entry => entry.EntryKey, entry => ContentDigest.OfJson(entry), StringComparer.Ordinal);
        var after = entries.ToDictionary(entry => entry.EntryKey, entry => ContentDigest.OfJson(entry), StringComparer.Ordinal);
        return before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).Where(key =>
            !before.TryGetValue(key, out var oldDigest) || !after.TryGetValue(key, out var newDigest) || oldDigest != newDigest)
            .Order(StringComparer.Ordinal).ToArray();
    }
    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Publication requires the exact committed approval of the named template's separate draft."));
}
