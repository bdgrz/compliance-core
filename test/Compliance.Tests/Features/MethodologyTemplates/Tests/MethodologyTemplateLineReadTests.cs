using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateLineReadTests
{
    [Fact]
    public async Task ShouldReadIdleLineWithoutInventedDraftGivenCommittedCreation()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var view = await fixture.Views.ReadAsync(fixture.Template);
        var publication = await fixture.Views.PublicationAsync(fixture.Template);

        // Assert
        Assert.NotNull(view);
        Assert.Equal("Firm methodology", view.Name);
        Assert.Equal("Original firm entries", view.Purpose);
        Assert.Equal("idle", view.Status);
        Assert.Equal(fixture.Operator, view.CreatedBy);
        Assert.Null(view.OpenDraftId);
        Assert.Equal(0, view.PublishedVersion);
        Assert.Null(view.PublishedVersionId);
        Assert.NotNull(publication);
        Assert.Null(publication.Version);
    }

    [Fact]
    public async Task ShouldReadActualApprovalAndVerifiedEntriesGivenPublishedSeparateDraft()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var fact = await fixture.PublishAsync("First entries");

        // Act
        var view = await fixture.Views.VersionAsync(fixture.Template, 1);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(fact.DraftId, view.DraftId);
        Assert.Equal(fact.DraftRevision, view.DraftRevision);
        Assert.Equal(fact.ApprovalEventId, view.ApprovalEventId);
        Assert.Equal(fact.ApprovedBy, view.ApprovedBy);
        Assert.Equal(fact.Metadata.OccurredOn, view.PublishedAt);
        Assert.Equal(fact.EntriesContent, view.EntriesContent);
        Assert.Equal("First entries", Assert.Single(view.Entries).Title);
        Assert.Equal(fact.ChangedEntryKeys, view.ChangedEntryKeys);
        Assert.Equal(fact.Metadata.EventId, view.PublicationSource.EventId);
        Assert.Equal(new MethodologyTemplate(fixture.Template).Stream, view.PublicationSource.Stream);
        Assert.Equal(2UL, view.PublicationPosition);
    }

    [Fact]
    public async Task ShouldPreserveEarlierImmutableVersionGivenLaterPublication()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.PublishAsync("First entries");
        var second = await fixture.PublishAsync("Second entries");

        // Act
        var prior = await fixture.Views.VersionAsync(fixture.Template, 1);
        var latest = await fixture.Views.PublicationAsync(fixture.Template);
        var future = await fixture.Views.VersionAsync(fixture.Template, 3);

        // Assert
        Assert.NotNull(prior);
        Assert.Equal(first.EntriesContent, prior.EntriesContent);
        Assert.Equal("First entries", Assert.Single(prior.Entries).Title);
        Assert.NotNull(latest);
        Assert.NotNull(latest.Version);
        Assert.Equal(2, latest.Line.PublishedVersion);
        Assert.Equal(second.EntriesContent, latest.Version.EntriesContent);
        Assert.Equal("Second entries", Assert.Single(latest.Version.Entries).Title);
        Assert.Null(future);
    }

    [Fact]
    public async Task ShouldKeepLatestPublicationWhileShowingOpenDraftGivenNextDraftStarted()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.PublishAsync("First entries");
        var owner = await fixture.OwnerAsync();
        var draft = Uuid.CreateVersion4();
        var entries = await fixture.Source.Content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(
            MethodologyTemplate.Realm, first.EntriesContent, CancellationToken.None);
        Assert.True(owner.StartDraft(draft, 1, entries, first.EntriesContent, fixture.Operator).IsSuccess);
        await fixture.SaveAsync(owner);

        // Act
        var view = await fixture.Views.PublicationAsync(fixture.Template);

        // Assert
        Assert.NotNull(view);
        Assert.Equal("drafting", view.Line.Status);
        Assert.Equal(draft, view.Line.OpenDraftId);
        Assert.NotNull(view.Version);
        Assert.Equal(first.EntriesContent, view.Version.EntriesContent);
        Assert.Equal(1, view.Version.Version);
    }

    [Fact]
    public async Task ShouldExcludeLaterPublicationGivenCapturedSourceCut()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PublishAsync("First entries");
        await fixture.PublishAsync("Later entries");
        var snapshots = new ChangedSnapshot(new EventStoreSnapshotReader(fixture.Source.Events),
            cut: EventStoreSourceFactResolver.GlobalBase + 3);
        var views = new MethodologyTemplateLineReader(snapshots, fixture.Source.Content);

        // Act
        var view = await views.PublicationAsync(fixture.Template);
        var later = await views.VersionAsync(fixture.Template, 2);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(1, view.Line.PublishedVersion);
        Assert.NotNull(view.Version);
        Assert.Equal("First entries", Assert.Single(view.Version.Entries).Title);
        Assert.Null(later);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("realm")]
    [InlineData("payload")]
    public async Task ShouldFailClosedGivenIncompleteOrForgedNativeSnapshot(string fault)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PublishAsync("First entries");
        var views = new MethodologyTemplateLineReader(new ChangedSnapshot(new EventStoreSnapshotReader(fixture.Source.Events), fault),
            fixture.Source.Content);

        // Act
        var error = await Assert.ThrowsAsync<SourceFactVerificationException>(() => views.PublicationAsync(fixture.Template).AsTask());

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ShouldRefuseUnverifiedEntriesGivenPublishedContentUnavailable()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PublishAsync("First entries");
        var views = new MethodologyTemplateLineReader(new EventStoreSnapshotReader(fixture.Source.Events), new UnavailableContent(fixture.Source.Content));

        // Act
        var error = await Assert.ThrowsAsync<ImmutableContentUnavailableException>(() => views.VersionAsync(fixture.Template, 1).AsTask());

        // Assert
        Assert.Contains("Unavailable", error.Message, StringComparison.Ordinal);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public MethodologyTemplateScenario Source { get; } = new(DateTimeOffset.UnixEpoch);
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public Uuid Operator { get; } = Uuid.CreateVersion4();
        public MethodologyTemplateLineReader Views => new(new EventStoreSnapshotReader(Source.Events), Source.Content);
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var owner = new MethodologyTemplate(fixture.Template);
            Assert.True(owner.CreateLine("Firm methodology", "Original firm entries", fixture.Operator).IsSuccess);
            await fixture.SaveAsync(owner);
            return fixture;
        }
        public async Task<MethodologyTemplateVersionPublishedV4> PublishAsync(string title)
        {
            var owner = await OwnerAsync();
            var draft = Uuid.CreateVersion4();
            IReadOnlyList<MethodologyTemplateRecordContent> before = owner.PublishedEntriesContent is { } prior
                ? await Source.Content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(MethodologyTemplate.Realm, prior, CancellationToken.None)
                : [];
            var opening = owner.PublishedEntriesContent ?? await Source.Content.WriteJsonAsync(MethodologyTemplate.Realm,
                Uuid.CreateVersion4(), MethodologyTemplateDraft.EntriesSchema, before, CancellationToken.None);
            Assert.True(owner.StartDraft(draft, owner.PublishedVersionNumber, before, opening, Operator).IsSuccess);
            IReadOnlyList<MethodologyTemplateRecordContent> entries = [new("access-review", "control", title, "Review quarterly.", [])];
            var content = await Source.Content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                MethodologyTemplateDraft.EntriesSchema, entries, CancellationToken.None);
            Assert.True(owner.PublishDraft(draft, Uuid.CreateVersion4(), 1, owner.PublishedVersionNumber, entries,
                content, ["access-review"], Operator).IsSuccess);
            await SaveAsync(owner);
            return Assert.IsType<MethodologyTemplateVersionPublishedV4>((await Source.FactsAsync(Template))[^1]);
        }
        public ValueTask<MethodologyTemplate> OwnerAsync() => Source.Reader.HydrateAsync(new MethodologyTemplate(Template));
        public ValueTask SaveAsync(MethodologyTemplate owner) => Source.Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }

    sealed class ChangedSnapshot(IAuthoritativeSnapshotReader inner, string? fault = null, ulong? cut = null) : IAuthoritativeSnapshotReader
    {
        public async Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default)
        {
            var captured = await inner.CaptureAsync(ct);
            return cut is { } watermark ? captured with { Cut = captured.Cut with { GlobalWatermarkExclusive = watermark } } : captured;
        }
        public async Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope,
            AuthoritativeReadCut captured, SnapshotCursor? cursor = null, CancellationToken ct = default)
        {
            var read = await inner.ReadAtAsync(selector, tenantScope, captured, cursor, ct);
            if (fault == "incomplete")
                return read with { IsComplete = false };
            if (fault is not ("realm" or "payload"))
                return read;
            return read with { Facts = read.Facts.Select((fact, index) => index == 0 ? fact with
            {
                Locator = fault == "realm" ? fact.Locator with
                {
                    Source = fact.Locator.Source with { Realm = "other-realm" },
                } : fact.Locator with { PayloadSha256 = new string('0', 64) },
            } : fact).ToArray() };
        }
    }
    sealed class UnavailableContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => throw new ImmutableContentUnavailableException("Unavailable immutable entry content.");
    }
}
