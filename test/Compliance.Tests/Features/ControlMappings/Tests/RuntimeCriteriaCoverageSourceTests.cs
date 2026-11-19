using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

public sealed class RuntimeCriteriaCoverageSourceTests
{
    [Fact]
    public async Task ShouldConfirmDiscoveryGivenExactPublicationAndSettledProjectedEntry()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var covered = await fixture.Source.CoversAsync(fixture.Edition.Id, fixture.Publication, CancellationToken.None);

        // Assert
        Assert.True(covered);
        Assert.Equal(1, fixture.Entries.Reads);
    }

    [Fact]
    public async Task ShouldStayPendingGivenPublicationMarkerNotObserved()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Reader.IncludePublication = false;

        // Act
        // Assert
        Assert.False(await fixture.Source.CoversAsync(fixture.Edition.Id, fixture.Publication, CancellationToken.None));
    }

    [Fact]
    public async Task ShouldStayPendingGivenEntryOutcomeIsUnresolved()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Entries.Pending = true;

        // Act
        // Assert
        Assert.False(await fixture.Source.CoversAsync(fixture.Edition.Id, fixture.Publication, CancellationToken.None));
    }

    [Fact]
    public async Task ShouldStayPendingGivenProjectionNamesAnotherIncarnation()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Runtime.Row = fixture.Runtime.Row with { IncarnationId = Uuid.CreateVersion4() };

        // Act
        // Assert
        Assert.False(await fixture.Source.CoversAsync(fixture.Edition.Id, fixture.Publication, CancellationToken.None));
    }

    sealed class Fixture
    {
        public CriteriaEdition Edition { get; } = new(Uuid.CreateVersion4());
        public CriteriaPublicationProof Publication { get; }
        public Reader Reader { get; }
        public Runtime Runtime { get; }
        public Entries Entries { get; }
        public RuntimeCriteriaCoverageSource Source { get; }

        public Fixture()
        {
            var draft = Uuid.CreateVersion4();
            var operation = Uuid.CreateVersion4();
            var incarnation = Uuid.CreateVersion4();
            var entry = new CriteriaEntry(Edition.Id, "CC1.1");
            var intent = new CriterionChangeIntent(Edition.Id, draft, "CC1.1", operation, 0, "create", "criterion",
                "security", null, null, "summary");
            var proposal = new CriterionChangeProposed(intent, 1, incarnation, new ActorAttribution("operator", "bdgrz"),
                null!, null, Uuid.CreateVersion4());
            proposal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), entry.Id, 1, DateTimeOffset.UtcNow));
            var published = new CriteriaEditionPublished(Edition.Id, draft, Uuid.CreateVersion4(), 1, "CC1.1", incarnation,
                "edition_1", 1, Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ActorAttribution("operator", "bdgrz"));
            published.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), Edition.Id, 1, DateTimeOffset.UtcNow));
            Publication = new CriteriaPublicationProof(Edition.Id, published.Metadata.EventId, 0);
            var criterion = new Criterion(Edition.Id, "CC1.1", "CC1.1", "security", "criterion", null, "summary");
            var proof = new CriteriaEntryProof(criterion, incarnation, Publication.EventId, proposal.Metadata.EventId,
                Uuid.CreateVersion4(), null, null, null);
            Reader = new Reader(new DomainEventRecord(entry.Stream, proposal, 0, EventCursor.Start),
                new DomainEventRecord(Edition.Stream, published, 0, EventCursor.Start));
            Runtime = new Runtime(new RuntimeCriteriaEntry(Edition.Id, "CC1.1", "accepted", incarnation,
                "criterion", "security", null, null, "current summary"));
            Entries = new Entries(proof);
            Source = new RuntimeCriteriaCoverageSource(Reader, Runtime, Entries, new Publications(Publication));
        }
    }

    sealed class Reader(DomainEventRecord proposal, DomainEventRecord publication) : IDomainEventReader
    {
        public bool IncludePublication { get; set; } = true;
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset, CancellationToken ct) =>
            throw new NotSupportedException();
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            yield return proposal;
            if (IncludePublication)
                yield return publication;
        }
    }

    sealed class Entries(CriteriaEntryProof proof) : ICriteriaEntryAuthority
    {
        public bool Pending { get; set; }
        public int Reads { get; private set; }
        public ValueTask<Result<CriteriaEntryAuthorityResult>> ReadAsync(Uuid editionId, string identifier, CancellationToken ct = default)
        {
            Reads++;
            return ValueTask.FromResult(Result<CriteriaEntryAuthorityResult>.Success(Pending
                ? new(CriteriaEntryAuthorityStatus.Pending, null) : new(CriteriaEntryAuthorityStatus.Accepted, proof)));
        }
    }

    sealed class Publications(CriteriaPublicationProof proof) : ICriteriaPublicationAuthority
    {
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Success(proof));
    }

    sealed class Runtime(RuntimeCriteriaEntry row) : IRuntimeCriteriaCatalogReader
    {
        public RuntimeCriteriaEntry Row { get; set; } = row;
        public ValueTask<Result<RuntimeCriteriaEntry>> GetEntryAsync(Uuid editionId, string identifier, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<RuntimeCriteriaEntry>.Success(Row));
        public ValueTask<Result<RuntimeCriteriaEdition>> GetEditionAsync(Uuid editionId, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Result<RuntimeCriteriaEditionsPage>> ListEditionsAsync(Uuid tenantId, int? limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Result<RuntimeCriteriaDisclosurePage>> ListDisclosuresAsync(Uuid tenantId, Uuid editionId, int? limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Result<RuntimeCriteriaPage>> ListEntriesAsync(Uuid tenantId, Uuid editionId, string? category, string? kind,
            string? parentIdentifier, int? limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
