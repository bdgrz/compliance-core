using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Reconstructs immutable template version history from an exact native source cut, never a directory.</summary>
public sealed class MethodologyTemplateReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    public const string Schema = "methodology-template-delta.v1";

    public async ValueTask<MethodologyTemplateView?> ReadAsync(Uuid templateId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new MethodologyTemplate(templateId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, ct).ConfigureAwait(false);
    }

    internal async ValueTask<MethodologyTemplateView?> ReadAsync(MethodologyTemplate owner, CancellationToken ct)
    {
        if (owner.Revision == 0)
            return null;
        var versions = new List<MethodologyTemplateVersionView>();
        MethodologyTemplateView? view = null;
        long revision = 0;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(MethodologyTemplate.Realm,
                           MethodologyTemplate.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not IMethodologyTemplateFact fact || fact.SubjectId != owner.Id ||
                fact.TenantId != Uuid.Empty || fact.Revision != revision + 1 || fact.Change.Schema != Schema)
                throw new ImmutableContentIntegrityException("The template source has an invalid exact history delta.");
            if (fact.Revision > owner.Revision)
                break;
            var delta = await content.ReadJsonAsync<MethodologyTemplateView>(MethodologyTemplate.Realm, fact.Change, ct)
                .ConfigureAwait(false);
            if (delta.TemplateId != owner.Id || delta.Revision != fact.Revision || delta.UpdatedAt != fact.OccurredAt ||
                delta.ActorMemberId != fact.ActorMemberId || delta.Versions is null)
                throw new ImmutableContentIntegrityException("A template delta differs from its native source revision.");
            if (record.Event is MethodologyTemplateRetired)
            {
                if (delta.Versions.Count != 0 || delta.Status != "retired")
                    throw new ImmutableContentIntegrityException("Retirement is metadata, not replacement version history.");
            }
            else
            {
                if (delta.Versions.Count != 1)
                    throw new ImmutableContentIntegrityException("A template version delta must contain exactly one version.");
                var version = delta.Versions[0];
                if (version.TemplateId != owner.Id || version.VersionNumber != fact.VersionNumber ||
                    version.VersionId != fact.VersionId || version.Status != fact.VersionStatus ||
                    version.ContentSha256 != fact.ContentSha256 || version.AuthoredByMemberId != fact.AuthorMemberId ||
                    version.Review?.ReviewedByMemberId != fact.ReviewerMemberId)
                    throw new ImmutableContentIntegrityException("Template content differs from the authoritative version pointer.");
                var index = checked((int)version.VersionNumber - 1);
                if (record.Event is MethodologyTemplateCreated or MethodologyTemplateDraftRevised)
                {
                    if (index != versions.Count)
                        throw new ImmutableContentIntegrityException("A new immutable version cannot replace another version.");
                    if (versions.Count > 0 && versions[^1].Status is "draft" or "changes_requested")
                        versions[^1] = versions[^1] with { Status = "superseded" };
                    versions.Add(version);
                }
                else
                {
                    if (index != versions.Count - 1 || versions[index].VersionId != version.VersionId ||
                        versions[index].ContentSha256 != version.ContentSha256)
                        throw new ImmutableContentIntegrityException("A review or publication cannot rewrite predecessor content.");
                    versions[index] = version;
                }
            }
            var next = delta with { Versions = versions.ToArray() };
            var transition = record.Event switch
            {
                MethodologyTemplateCreated => "create",
                MethodologyTemplateDraftRevised => "revise",
                MethodologyTemplateDraftReviewed => "review",
                MethodologyTemplateVersionPublished => "publish",
                MethodologyTemplateRetired => "retire",
                _ => throw new ImmutableContentIntegrityException("The template history has another transition."),
            };
            if (!MethodologyTemplateRules.ValidReadTransition(view, next, fact.ActorMemberId, transition))
                throw new ImmutableContentIntegrityException("The template history rewrites an immutable version or its attribution.");
            view = next;
            revision = fact.Revision;
            if (revision == owner.Revision)
                break;
        }
        if (revision != owner.Revision || view is null)
            throw new ImmutableContentUnavailableException("The template source cut is not fully visible yet.");
        if (!new MethodologyTemplateRules(owner.Id, view).HasValidCurrent)
            throw new ImmutableContentIntegrityException("The composed template violates its immutable version rules.");
        return view;
    }

    public async ValueTask<MethodologyTemplateVersionView?> VersionAsync(Uuid templateId, long version,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new MethodologyTemplate(templateId), ct).ConfigureAwait(false);
        return await VersionAsync(owner, version, ct).ConfigureAwait(false);
    }

    public async ValueTask<MethodologyTemplatePublicationSnapshot> PublicationAsync(Uuid templateId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new MethodologyTemplate(templateId), ct).ConfigureAwait(false);
        var version = owner.PublishedVersionNumber == 0 ? null
            : await VersionAsync(owner, owner.PublishedVersionNumber, ct).ConfigureAwait(false);
        if (version is not null && (version.VersionId != owner.PublishedVersionId || version.Status != "published"))
            throw new ImmutableContentIntegrityException("The publication differs from its exact owner pointer.");
        return new MethodologyTemplatePublicationSnapshot(owner, version);
    }

    async ValueTask<MethodologyTemplateVersionView?> VersionAsync(MethodologyTemplate owner, long version,
        CancellationToken ct)
    {
        if (version < 1 || owner.Revision == 0 || version > owner.VersionNumber)
            return null;
        IMethodologyTemplateFact? selected = null;
        long revision = 0;
        var sealedBySuccessor = false;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(MethodologyTemplate.Realm,
                           MethodologyTemplate.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not IMethodologyTemplateFact fact || fact.SubjectId != owner.Id ||
                fact.Revision != revision + 1 || fact.TenantId != Uuid.Empty)
                throw new ImmutableContentIntegrityException("The version lookup has another exact native source.");
            revision = fact.Revision;
            if (fact.VersionNumber > version)
            {
                sealedBySuccessor = true;
                break;
            }
            if (fact.VersionNumber == version && record.Event is not MethodologyTemplateRetired)
                selected = fact;
            if (revision == owner.Revision)
                break;
        }
        if (selected is null || !sealedBySuccessor && revision != owner.Revision)
            throw new ImmutableContentUnavailableException("The exact version source is not fully visible yet.");
        if (selected.Change.Schema != Schema)
            throw new ImmutableContentIntegrityException("The selected version has another immutable content schema.");
        var delta = await content.ReadJsonAsync<MethodologyTemplateView>(MethodologyTemplate.Realm, selected.Change, ct)
            .ConfigureAwait(false);
        if (delta.Versions is null)
            throw new ImmutableContentIntegrityException("The exact version content has no versions collection.");
        var result = delta.Versions.Count == 1 ? delta.Versions[0] : null;
        if (delta.Status != "active" || delta.TemplateId != owner.Id || delta.Revision != selected.Revision ||
            delta.UpdatedAt != selected.OccurredAt || delta.ActorMemberId != selected.ActorMemberId ||
            result is null || result.VersionNumber != version || result.VersionId != selected.VersionId ||
            result.Status != selected.VersionStatus || result.AuthoredByMemberId != selected.AuthorMemberId ||
            result.Review?.ReviewedByMemberId != selected.ReviewerMemberId || result.ContentSha256 != selected.ContentSha256 ||
            !MethodologyTemplateRules.ValidVersion(result, owner.Id) ||
            selected is MethodologyTemplateVersionPublished &&
            (result.ApprovedByMemberId != selected.ActorMemberId || result.ApprovedAt != selected.OccurredAt))
            throw new ImmutableContentIntegrityException("The selected immutable version differs from its native publication.");
        return result;
    }
}
