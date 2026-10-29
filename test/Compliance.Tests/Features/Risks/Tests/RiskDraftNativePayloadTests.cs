using Bdgrz.Compliance.Hosting.Persistence;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskDraftNativePayloadTests
{
    [Fact]
    public async Task ShouldAcceptMaximumValidEscapedNarrativeGivenCompactRiskDraftStorage()
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-ESCAPED");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        var request = new CreateRiskDraft(id, tenant, program, "R-ESCAPED", new string('t', 200), new string('\u0001', 8000), new string('\u0001', 8000), new string('\u0001', 4000));

        // Act
        var result = await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(request, user), CancellationToken.None);
        var owner = await fixture.Reader.HydrateAsync(new RiskDraft(tenant, id));
        var source = await fixture.Drafts.ReadAsync(tenant, program, id);
        var fact = Assert.IsType<RiskDraftCreated>(Assert.Single(await fixture.FactsAsync(tenant, id)));
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient(), fixture.Content, fixture.Drafts);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(FitzRiskDraftDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area)), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(fact);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var projected = await directory.GetAsync(tenant, id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(request.Scenario, source.Current!.Content.Scenario);
        Assert.Equal(request.PotentialEffect, projected!.Content.PotentialEffect);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(source.Current, ComplianceCoreJsonContext.Default.RiskDraftRevisionView).Length > RiskDraft.MaximumDraftEventPayloadBytes);
        Assert.True(DomainEventPayload.Size(fact) < 12 * 1024);
        var response = await FirstResponses.ReplayAsync(fixture.Content, tenant.ToString(), fact.FirstResponse!);
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(RiskDraftOperations.RegistrationSchema, response.ResultSchema);
    }
}
