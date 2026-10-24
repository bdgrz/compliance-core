using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

[Trait("Category", "WebIntegration")]
public sealed class CommitmentHttpRoutesTests
{
    [Fact]
    public async Task ShouldBindDistinctRoutesAndReplayOriginalResultsGivenCommitmentHttpLifecycle()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        using var reviewerClient = factory.CreateClient();
        using var approverClient = factory.CreateClient();
        var author = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "commitment-http-author@example.test"), CultureInfo.InvariantCulture);
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var id = CommitmentDraft.IdFor(tenant, program, "service_commitment", "SC-HTTP");
        await ProgramManagementServices.SeedAsync(factory.Services, new ComplianceProgram(tenant, program), owner =>
        {
            Assert.Null(owner.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(tenant, author), "Author", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(factory.Services, new ClientService(tenant, service), owner =>
            owner.Create(program, "Service", "Purpose", "Owner", RbacIds.Member(tenant, author), "Author", DateTimeOffset.UtcNow));
        var path = $"/api/v1/tenants/{tenant}/programs/{program}/commitment-drafts/{id}";
        var create = new { service_id = service.ToString(), kind = "service_commitment", identifier = "SC-HTTP", statement = "First statement", context = "Context", source_reference = "MSA" };
        var revision = Uuid.CreateVersion4();
        var revise = new { expected_revision = 1, statement = "Revised statement", context = "Context", source_reference = "MSA" };
        var review = Uuid.CreateVersion4();
        var approval = Uuid.CreateVersion4();
        var retirement = Uuid.CreateVersion4();

        // Act
        using var created = await client.PutAsJsonAsync(path, create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var createReplay = await client.PutAsJsonAsync(path, create);
        Assert.Equal(HttpStatusCode.OK, createReplay.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await createReplay.Content.ReadAsStringAsync());
        using var revised = await client.PutAsJsonAsync($"{path}/draft-revisions/{revision}", revise);
        using var revisionReplay = await client.PutAsJsonAsync($"{path}/draft-revisions/{revision}", revise);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revisionReplay.StatusCode);
        await CatchUpAsync(factory.Services, tenant);
        using var historical = await client.GetAsync($"{path}/revisions/1");
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        using var original = JsonDocument.Parse(await historical.Content.ReadAsStringAsync());
        Assert.Equal("First statement", original.RootElement.GetProperty("statement").GetString());
        var reviewer = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(reviewerClient, "commitment-http-reviewer@example.test"), CultureInfo.InvariantCulture);
        Assert.NotEqual(author, reviewer);
        using var reviewed = await reviewerClient.PutAsJsonAsync($"{path}/reviews/{review}", new
        {
            expected_revision = 2,
            outcome = "accept",
            rationale = "Verified source",
            owner_reference = "Owner",
            applicability = "applicable",
            interpretation = "supported",
            source_verified_reference = "MSA",
            source_evidence = "Signed MSA checked",
        });
        Assert.True(reviewed.StatusCode == HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());
        await CatchUpAsync(factory.Services, tenant);
        var approver = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(approverClient, "commitment-http-approver@example.test"), CultureInfo.InvariantCulture);
        Assert.NotEqual(author, approver);
        Assert.NotEqual(reviewer, approver);
        using var preview = await client.GetAsync($"{path}/impact-preview?expected_revision=2");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var impact = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        using var approved = await approverClient.PutAsJsonAsync($"{path}/approvals/{approval}", new
        {
            expected_revision = 2,
            accepted_review_decision_id = review.ToString(),
            effective_from = "2027-01-01",
            rationale = "Approved",
            impact_digest = impact.RootElement.GetProperty("digest").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var retirementPath = $"/api/v1/tenants/{tenant}/programs/{program}/commitments/{id}/retirements/{retirement}";
        var retire = new { expected_revision = 2, effective_on = "2027-02-01", rationale = "Terms superseded" };
        using var retired = await client.PutAsJsonAsync(retirementPath, retire);
        var accepted = await ProgramManagementServices.HydrateAsync(factory.Services, new CommitmentDraft(tenant, id));
        using var retirementReplay = await client.PutAsJsonAsync(retirementPath, retire);
        using var wrongScope = await client.PutAsJsonAsync($"/api/v1/tenants/{tenant}/programs/{Uuid.CreateVersion4()}/commitments/{id}/retirements/{retirement}", retire);
        var current = await ProgramManagementServices.HydrateAsync(factory.Services, new CommitmentDraft(tenant, id));

        // Assert
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retirementReplay.StatusCode);
        Assert.Equal("{}", await revised.Content.ReadAsStringAsync());
        Assert.Equal("{}", await revisionReplay.Content.ReadAsStringAsync());
        Assert.Equal("{}", await retired.Content.ReadAsStringAsync());
        Assert.Equal("{}", await retirementReplay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, wrongScope.StatusCode);
        Assert.True(current.IsRetired);
        Assert.Equal(accepted.CommittedStreamPosition, current.CommittedStreamPosition);
        Assert.Equal(2, current.Revision);
    }

    static async Task CatchUpAsync(IServiceProvider provider, Uuid tenant)
    {
        await using var scope = provider.CreateAsyncScope();
        var projection = scope.ServiceProvider.GetRequiredService<ICommitmentDraftDirectoryProjection>();
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), "commitment-drafts");
        var name = typeof(FitzCommitmentDraftDirectory).GetField("ProjectorName")?.GetRawConstantValue() as string ?? "CommitmentDraftDirectory";
        var previous = await scope.ServiceProvider.GetRequiredService<ICommitmentDraftDirectoryReader>().LoadCheckpointAsync(tenant);
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(name, pattern), previous));
        var cursor = previous.Cursor;
        await foreach (var record in provider.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await projection.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
