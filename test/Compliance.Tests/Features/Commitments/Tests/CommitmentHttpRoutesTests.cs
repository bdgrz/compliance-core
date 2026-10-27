using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Projections;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

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
        await ProgramManagementServices.SeedAsync(factory.Services, new ProgramRules(tenant, program), owner =>
        {
            Assert.Null(owner.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(tenant, author), "Author", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(factory.Services, new ProductClientServiceRules(tenant, service), owner =>
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
