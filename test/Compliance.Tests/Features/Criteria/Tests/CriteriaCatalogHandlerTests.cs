using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaCatalogHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));
    static readonly Uuid EditionId = CriteriaCatalog.Platform.Edition.EditionId;

    [Fact]
    public async Task ShouldWalkFilteredPagesAndRejectTransplantedCursorGivenOtherTenant()
    {
        // Arrange
        var handler = new ListCriteriaCatalogEntriesHandler(new FixtureRuntimeCatalog(),
            new EmptyCriteriaTextOverlayReader());
        var request = new ListCriteriaCatalogEntries(TenantId, EditionId, "security",
            "criterion", Limit: 10);

        // Act
        var identifiers = new List<string>();
        string? firstCursor = null;
        string? cursor = null;
        do
        {
            var page = await ListAsync(handler, request with { Cursor = cursor });
            Assert.True(page.IsSuccess);
            identifiers.AddRange(page.Value!.Items.Select(static entry => entry.Identifier));
            cursor = page.Value.NextCursor;
            firstCursor ??= cursor;
        } while (cursor is not null);
        var otherTenant = await ListAsync(handler, request with
        {
            TenantId = Uuid.CreateVersion4(),
            Cursor = firstCursor,
        });
        var otherFilter = await ListAsync(handler, request with
        {
            Category = "privacy",
            Cursor = firstCursor,
        });

        // Assert
        Assert.Equal(33, identifiers.Count);
        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(RequestErrorKind.Validation, otherTenant.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, otherFilter.Error?.Kind);
    }

    [Theory]
    [InlineData("governance", null, 50)]
    [InlineData(null, "control", 50)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, 201)]
    public async Task ShouldRejectListGivenUnknownFilterOrLimit(string? category, string? kind,
        int limit)
    {
        // Arrange
        var handler = new ListCriteriaCatalogEntriesHandler(new FixtureRuntimeCatalog(),
            new EmptyCriteriaTextOverlayReader());

        // Act
        var result = await ListAsync(handler, new ListCriteriaCatalogEntries(TenantId, EditionId,
            category, kind, Limit: limit));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownEditionOrIdentifier()
    {
        // Arrange
        var entries = new GetCriteriaCatalogEntryHandler(new FixtureRuntimeCatalog(),
            new EmptyCriteriaTextOverlayReader());
        var editions = new GetCriteriaCatalogEditionHandler(new FixtureRuntimeCatalog());

        // Act
        var known = await entries.HandleAsync(new RequestContext<GetCriteriaCatalogEntry>(
            new GetCriteriaCatalogEntry(TenantId, EditionId, "CC6.1"), Actor),
            CancellationToken.None);
        var unknownIdentifier = await entries.HandleAsync(new RequestContext<GetCriteriaCatalogEntry>(
            new GetCriteriaCatalogEntry(TenantId, EditionId, "CC6.9"), Actor),
            CancellationToken.None);
        var unknownEdition = await editions.HandleAsync(new RequestContext<GetCriteriaCatalogEdition>(
            new GetCriteriaCatalogEdition(TenantId, Uuid.CreateVersion4()), Actor),
            CancellationToken.None);

        // Assert
        Assert.Equal("CC6.1", known.Value?.SourceIdentifier);
        Assert.Equal(RequestErrorKind.NotFound, unknownIdentifier.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknownEdition.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReportPendingGivenUnsettledRuntimeEntry()
    {
        // Arrange
        var handler = new GetCriteriaCatalogEntryHandler(new FixtureRuntimeCatalog { Pending = true },
            new EmptyCriteriaTextOverlayReader());

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetCriteriaCatalogEntry>(
            new GetCriteriaCatalogEntry(TenantId, EditionId, "CC6.1"), Actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectDisplayCursorGivenExportRequest()
    {
        // Arrange
        var catalog = new FixtureRuntimeCatalog();
        var overlays = new EmptyCriteriaTextOverlayReader();
        var display = new ListCriteriaCatalogEntriesHandler(catalog, overlays);
        var export = new ExportCriteriaCatalogEntriesHandler(catalog, overlays);
        var first = await ListAsync(display, new ListCriteriaCatalogEntries(TenantId, EditionId,
            "security", "criterion", Limit: 1));

        // Act
        var result = await export.HandleAsync(new RequestContext<ExportCriteriaCatalogEntries>(
            new ExportCriteriaCatalogEntries(TenantId, EditionId, "security", "criterion",
                Limit: 1, Cursor: first.Value!.NextCursor), Actor), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value!.NextCursor);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldKeepLegacyEditionListPendingGivenUnqualifiedReader()
    {
        // Arrange
        var handler = new ListCriteriaCatalogEditionsHandler(new FixtureRuntimeCatalog());

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListCriteriaCatalogEditions>(
            new ListCriteriaCatalogEditions(TenantId), Actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnCatalogEntryWithoutOverlayGivenEmptyProjection()
    {
        // Arrange
        var directory = new FitzCriteriaTextOverlayDirectory(new InMemoryKvClient(),
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
        var overlays = new FitzCriteriaTextOverlayReader(directory,
            new CriteriaTextOverlayReadConsistency(directory, new EmptyEventReader()));
        var entry = CriteriaCatalog.Platform.GetEntry(EditionId, "CC6.1")!;

        // Act
        var result = await overlays.ApplyAsync(TenantId, entry, isExport: false,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(entry, result.Value);
    }

    static async Task<Result<Page<Criterion>>> ListAsync(ListCriteriaCatalogEntriesHandler handler,
        ListCriteriaCatalogEntries request) =>
        await handler.HandleAsync(new RequestContext<ListCriteriaCatalogEntries>(request, Actor),
            CancellationToken.None);

    // Test-only fixture for exercising legacy response compatibility; production
    // reads use the durable runtime projection, not this in-memory catalog.
    sealed class FixtureRuntimeCatalog : IRuntimeCriteriaCatalogReader
    {
        readonly Dictionary<string, Cursor> _cursors = [];
        readonly Uuid _generation = Uuid.CreateVersion4();
        readonly Uuid _publication = Uuid.CreateVersion4();
        public bool Pending { get; init; }
        public IReadOnlyList<CriteriaCatalogEdition>? LegacyEditions { get; init; }

        public ValueTask<Result<IReadOnlyList<CriteriaCatalogEdition>>> ListLegacyEditionsAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(LegacyEditions is null
                ? Result<IReadOnlyList<CriteriaCatalogEdition>>.Failure(new RequestError(RequestErrorKind.Conflict, "Pending.", true))
                : Result<IReadOnlyList<CriteriaCatalogEdition>>.Success(LegacyEditions));

        public ValueTask<Result<RuntimeCriteriaEdition>> GetEditionAsync(Uuid editionId,
            CancellationToken ct = default)
        {
            var item = CriteriaCatalog.Platform.GetEdition(editionId);
            return ValueTask.FromResult(item is null
                ? Result<RuntimeCriteriaEdition>.Failure(new RequestError(RequestErrorKind.NotFound, "Unknown edition."))
                : Result<RuntimeCriteriaEdition>.Success(new RuntimeCriteriaEdition(editionId, _generation,
                    "published", item.EditionLabel, _publication, item.PublishedAt, "complete")));
        }

        public ValueTask<Result<RuntimeCriteriaEntry>> GetEntryAsync(Uuid editionId, string identifier,
            CancellationToken ct = default)
        {
            if (Pending)
                return ValueTask.FromResult(Result<RuntimeCriteriaEntry>.Success(new RuntimeCriteriaEntry(editionId,
                    identifier, "pending", null, null, null, null, null, null)));
            var item = CriteriaCatalog.Platform.GetEntry(editionId, identifier);
            return ValueTask.FromResult(item is null
                ? Result<RuntimeCriteriaEntry>.Failure(new RequestError(RequestErrorKind.NotFound, "Unknown entry."))
                : Result<RuntimeCriteriaEntry>.Success(Convert(item)));
        }

        public ValueTask<Result<RuntimeCriteriaEditionsPage>> ListEditionsAsync(Uuid tenantId, int? limit,
            string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result<RuntimeCriteriaDisclosurePage>> ListDisclosuresAsync(Uuid tenantId,
            Uuid editionId, int? limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Result<RuntimeCriteriaPage>> ListEntriesAsync(Uuid tenantId, Uuid editionId,
            string? category, string? kind, string? parentIdentifier, int? limit, string? cursor,
            CancellationToken ct = default)
        {
            if (Pending)
                return ValueTask.FromResult(Result<RuntimeCriteriaPage>.Success(new RuntimeCriteriaPage(editionId,
                    _publication, _generation.ToString(), "pending", [], null)));
            var position = 0;
            if (cursor is not null)
            {
                if (!_cursors.TryGetValue(cursor, out var previous) || previous.TenantId != tenantId ||
                    previous.EditionId != editionId || previous.Category != category || previous.Kind != kind ||
                    previous.Parent != parentIdentifier)
                    return ValueTask.FromResult(Result<RuntimeCriteriaPage>.Failure(
                        new RequestError(RequestErrorKind.Validation, "Invalid cursor.")));
                position = previous.Position;
            }

            var all = CriteriaCatalog.Platform.ListEntries(editionId, category, kind, parentIdentifier);
            var items = all.Skip(position).Take(limit ?? 50).Select(Convert).ToArray();
            var nextPosition = position + items.Length;
            string? next = null;
            if (nextPosition < all.Count)
            {
                next = Uuid.CreateVersion4().ToString();
                _cursors.Add(next, new Cursor(tenantId, editionId, category, kind, parentIdentifier, nextPosition));
            }
            return ValueTask.FromResult(Result<RuntimeCriteriaPage>.Success(new RuntimeCriteriaPage(editionId,
                _publication, _generation.ToString(), "complete", items, next)));
        }

        static RuntimeCriteriaEntry Convert(Criterion item) => new(item.EditionId, item.Identifier, "accepted",
            Uuid.CreateVersion4(), item.Kind, item.Category, item.ParentIdentifier,
            item.Kind == "point_of_focus" ? Uuid.CreateVersion4() : null, item.Summary);

        sealed record Cursor(Uuid TenantId, Uuid EditionId, string? Category, string? Kind, string? Parent,
            int Position);
    }

    sealed class EmptyEventReader : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
