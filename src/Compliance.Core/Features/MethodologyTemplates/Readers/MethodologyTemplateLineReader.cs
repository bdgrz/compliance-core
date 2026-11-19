using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

/// <summary>Reads actual separate-draft line facts and verified published entries at one existing authoritative source cut.</summary>
public sealed class MethodologyTemplateLineReader(IAuthoritativeSnapshotReader snapshots, IImmutableContentStore content)
{
    public async ValueTask<MethodologyTemplateLineView?> ReadAsync(Uuid templateId, CancellationToken ct = default) =>
        (await ReadSourceAsync(templateId, null, ct).ConfigureAwait(false))?.Line;

    public async ValueTask<MethodologyTemplatePublishedVersionView?> VersionAsync(Uuid templateId, long version,
        CancellationToken ct = default)
    {
        if (version < 1)
            return null;
        var source = await ReadSourceAsync(templateId, version, ct).ConfigureAwait(false);
        return source?.Publication is { } publication ? await ReadVersionAsync(publication, ct).ConfigureAwait(false) : null;
    }

    public async ValueTask<MethodologyTemplatePublicationView?> PublicationAsync(Uuid templateId, CancellationToken ct = default)
    {
        var source = await ReadSourceAsync(templateId, null, ct).ConfigureAwait(false);
        if (source is null)
            return null;
        var version = source.Publication is { } publication ? await ReadVersionAsync(publication, ct).ConfigureAwait(false) : null;
        return new MethodologyTemplatePublicationView(source.Line, version);
    }

    internal async ValueTask<ResolvedSourceFact?> CreationSourceAsync(Uuid templateId, CancellationToken ct)
    {
        var capture = await snapshots.CaptureAsync(ct).ConfigureAwait(false);
        var source = await snapshots.ReadAtAsync(new SnapshotSelector(MethodologyTemplate.Area, templateId.ToString()),
            MethodologyTemplate.Realm, capture.Cut, null, ct).ConfigureAwait(false);
        if (!source.IsComplete)
            throw new SourceFactVerificationException("The identity creation source did not reach its captured cut.");
        var stream = new MethodologyTemplate(templateId).Stream;
        foreach (var record in source.Facts)
        {
            var metadata = record.Event.Metadata;
            if (record.Locator.Source != stream || record.Locator.GlobalPosition >= capture.Cut.GlobalWatermarkExclusive ||
                record.Locator.EventId != metadata.EventId || metadata.AggregateId != templateId || metadata.IsAudit ||
                record.Locator.AggregateVersion != metadata.AggregateVersion ||
                record.Locator.PayloadSha256 != ContentDigest.Sha256(record.Event, record.Event.GetType()))
                throw new SourceFactVerificationException("The identity creation source differs from its exact native tuple.");
            if (record.Event is MethodologyTemplateCreatedV4 or MethodologyTemplateCreated)
            {
                if (metadata.AggregateVersion != 1)
                    throw new SourceFactVerificationException("An immutable template identity must start with its creation.");
                return record;
            }
        }
        return null;
    }

    async ValueTask<LineSource?> ReadSourceAsync(Uuid templateId, long? requestedVersion, CancellationToken ct)
    {
        if (templateId == Uuid.Empty)
            return null;
        var capture = await snapshots.CaptureAsync(ct).ConfigureAwait(false);
        var source = await snapshots.ReadAtAsync(new SnapshotSelector(MethodologyTemplate.Area, templateId.ToString()),
            MethodologyTemplate.Realm, capture.Cut, null, ct).ConfigureAwait(false);
        if (!source.IsComplete)
            throw new SourceFactVerificationException("The methodology line source did not reach its captured cut.");
        var owner = new MethodologyTemplate(templateId);
        MethodologyTemplateCreatedV4? created = null;
        MethodologyTemplateRetiredV4? retired = null;
        DomainEvent? last = null;
        ResolvedSourceFact? selected = null;
        ulong? priorPosition = null;
        var eventIds = new HashSet<Uuid>();
        foreach (var record in source.Facts)
        {
            var fact = record.Event;
            var locator = record.Locator;
            var metadata = fact.Metadata;
            if (locator.Source != owner.Stream || locator.GlobalPosition >= capture.Cut.GlobalWatermarkExclusive ||
                locator.EventId == Uuid.Empty || locator.EventId != metadata.EventId || metadata.AggregateId != templateId ||
                locator.AggregateVersion != metadata.AggregateVersion || metadata.AggregateVersion == 0 || metadata.IsAudit ||
                metadata.OccurredOn == default ||
                locator.ResourcePosition < metadata.AggregateVersion - 1 ||
                priorPosition is { } prior && locator.ResourcePosition <= prior || !eventIds.Add(locator.EventId) ||
                locator.PayloadSha256 != ContentDigest.Sha256(fact, fact.GetType()))
                throw new SourceFactVerificationException("The methodology line source differs from its exact native tuple and payload.");
            if (owner.Revision == 0 && fact is MethodologyTemplateCreated)
                return null;
            try
            {
                owner.ReplayLineFact(fact);
            }
            catch (InvalidOperationException exception)
            {
                throw new ImmutableContentIntegrityException($"The methodology line has invalid native transitions: {exception.Message}");
            }
            if (fact is MethodologyTemplateCreatedV4 creation)
                created = creation;
            else if (fact is MethodologyTemplateVersionPublishedV4 published &&
                (requestedVersion is null || published.Version == requestedVersion))
                selected = record;
            else if (fact is MethodologyTemplateRetiredV4 retirement)
                retired = retirement;
            last = fact;
            priorPosition = locator.ResourcePosition;
        }
        if (created is null || last is null)
            return null;
        var line = new MethodologyTemplateLineView(templateId, created.Name, created.Purpose, owner.LineStatus,
            owner.Revision, owner.OpenDraftId, owner.PublishedVersionNumber,
            owner.PublishedVersionNumber == 0 ? null : owner.PublishedVersionId,
            created.CreatedBy, created.Metadata.OccurredOn, retired?.RetiredBy, retired?.Reason,
            retired?.Metadata.OccurredOn, last.Metadata.OccurredOn);
        return new LineSource(line, selected);
    }

    async ValueTask<MethodologyTemplatePublishedVersionView> ReadVersionAsync(ResolvedSourceFact source, CancellationToken ct)
    {
        if (source.Event is not MethodologyTemplateVersionPublishedV4 published)
            throw new ImmutableContentIntegrityException("The selected methodology source is not a separate-draft publication.");
        var entries = await content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm,
            published.EntriesContent, ct).ConfigureAwait(false);
        if (!MethodologyTemplateDraft.ValidManifest(entries, published.EntriesContent) ||
            !published.EntryKeys.SequenceEqual(entries.Select(entry => entry.EntryKey)))
            throw new ImmutableContentIntegrityException("Published methodology entries differ from their immutable native snapshot.");
        return new MethodologyTemplatePublishedVersionView(published.TemplateId,
            MethodologyTemplate.PublishedVersionIdentity(published.TemplateId, published.Version), published.Version,
            published.DraftId, published.DraftRevision, published.ApprovalEventId, published.ApprovedBy,
            published.Metadata.OccurredOn, published.EntriesContent,
            Array.AsReadOnly(entries.Select(MethodologyTemplateDestinationCommands.Freeze).ToArray()),
            Array.AsReadOnly(published.ChangedEntryKeys.ToArray()),
            NativeFactReference.Of(source.Locator.Source, published), source.Locator.ResourcePosition);
    }

    sealed record LineSource(MethodologyTemplateLineView Line, ResolvedSourceFact? Publication);
}
