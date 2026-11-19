using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class RuntimeCriteriaTransportContractTests
{
    [Fact]
    public void ShouldRequireExplicitFrameworkGivenDraftEditionWireContract()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var json = $"{{\"edition_id\":\"{edition}\",\"operation_id\":\"{operation}\",\"label\":\"edition_1\"}}";

        // Act
        var failure = Record.Exception(() => JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.DraftCriteriaEdition));

        // Assert
        Assert.IsType<JsonException>(failure);
    }

    [Fact]
    public void ShouldRejectMissingFrameworkGivenDraftedEventPayload()
    {
        // Arrange
        var fact = new CriteriaEditionDrafted(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "tsc", "edition_1", 1, Uuid.CreateVersion4());
        var json = JsonSerializer.Serialize(fact, ComplianceCoreJsonContext.Default.CriteriaEditionDrafted);
        var payload = JsonNode.Parse(json)!.AsObject();
        Assert.True(payload.Remove("framework"));

        // Act
        var failure = Record.Exception(() => JsonSerializer.Deserialize(payload.ToJsonString(),
            ComplianceCoreJsonContext.Default.CriteriaEditionDrafted));

        // Assert
        Assert.IsType<JsonException>(failure);
    }

    [Fact]
    public void ShouldRequirePlatformOperatorGivenAnyHumanCriteriaMutation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        IRequestBase[] writes =
        [
            new DraftCriteriaEdition(edition, operation, "tsc", "2017_tsc_2022_pof"),
            new ChooseInitialEditionLabel(edition, operation, "2017_tsc_2022_pof"),
            new CancelCriteriaEditionDraftRequest(edition, operation, Uuid.CreateVersion4()),
            new BeginCriteriaEditionRename(edition, operation, "2023_tsc"),
            new CancelCriteriaEditionRename(edition, operation, Uuid.CreateVersion4()),
            new AbandonCriteriaEditionDraft(edition, operation),
            new ProposeCriterionChange(edition, Uuid.CreateVersion4(), "CC6.1", operation, 0,
                "create", "criterion", "security", null, null, "Original summary"),
            new CorrectCriterionSummary(edition, "CC6.1", operation, 1, "Corrected summary"),
            new BeginCriteriaPublication(edition, operation, "CC6.1", Uuid.CreateVersion4()),
        ];

        // Act
        var operatorOnly = writes.All(static request => request is IPlatformOperatorRequest && request is ICallable);

        // Assert
        Assert.True(operatorOnly);
        Assert.All(writes, request => Assert.Equal(operation, Assert.IsAssignableFrom<IIdentifiedRequest>(request).RequestId));
    }

    [Fact]
    public async Task ShouldPreservePendingCoverageAndTenantBindingGivenRuntimePage()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var reader = new PendingReader();
        var handler = new ListRuntimeCriteriaEntriesHandler(reader);
        var request = new ListRuntimeCriteriaEntries(tenant, edition, "security", "criterion", null, 25, "cursor");
        var actor = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListRuntimeCriteriaEntries>(request, actor),
            CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<ITenantAccessRequest>(request);
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Coverage);
        Assert.Null(result.Value.NextCursor);
        Assert.Equal(tenant, reader.TenantId);
        Assert.Equal(edition, reader.EditionId);
        Assert.Equal("cursor", reader.Cursor);
    }

    [Fact]
    public async Task ShouldPreserveTenantAndCursorGivenDisclosurePageRequest()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var reader = new PendingReader();
        var handler = new ListRuntimeCriteriaDisclosuresHandler(reader);
        var request = new ListRuntimeCriteriaDisclosures(tenant, edition, 25, "disclosure-cursor");

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListRuntimeCriteriaDisclosures>(request,
            new ClaimsPrincipal(new ClaimsIdentity())), CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<ITenantAccessRequest>(request);
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Coverage);
        Assert.Equal(tenant, reader.TenantId);
        Assert.Equal(edition, reader.EditionId);
        Assert.Equal("disclosure-cursor", reader.Cursor);
    }

    sealed class PendingReader : IRuntimeCriteriaCatalogReader
    {
        public Uuid TenantId { get; private set; }
        public Uuid EditionId { get; private set; }
        public string? Cursor { get; private set; }

        public ValueTask<Result<RuntimeCriteriaEdition>> GetEditionAsync(Uuid editionId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result<RuntimeCriteriaEditionsPage>> ListEditionsAsync(Uuid tenantId,
            int? limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result<RuntimeCriteriaEntry>> GetEntryAsync(Uuid editionId, string identifier,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result<RuntimeCriteriaDisclosurePage>> ListDisclosuresAsync(Uuid tenantId,
            Uuid editionId, int? limit, string? cursor, CancellationToken ct = default)
        {
            TenantId = tenantId;
            EditionId = editionId;
            Cursor = cursor;
            return ValueTask.FromResult(Result<RuntimeCriteriaDisclosurePage>.Success(
                new RuntimeCriteriaDisclosurePage(editionId, "generation", "pending", [], null)));
        }

        public ValueTask<Result<RuntimeCriteriaPage>> ListEntriesAsync(Uuid tenantId, Uuid editionId,
            string? category, string? kind, string? parentIdentifier, int? limit, string? cursor,
            CancellationToken ct = default)
        {
            TenantId = tenantId;
            EditionId = editionId;
            Cursor = cursor;
            return ValueTask.FromResult(Result<RuntimeCriteriaPage>.Success(
                new RuntimeCriteriaPage(editionId, null, "generation", "pending", [], null)));
        }
    }
}
