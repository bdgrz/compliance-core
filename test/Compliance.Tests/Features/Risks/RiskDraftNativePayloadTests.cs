using Bdgrz.Compliance.Hosting.Persistence;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Risks;

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
