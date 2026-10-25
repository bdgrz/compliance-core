using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed partial class DefineAuditPopulationHandlerTests
{
    [Theory]
    [InlineData("define")]
    [InlineData("generate")]
    public async Task ShouldReplayOriginalUnitAfterClockAdvancesGivenAuditPopulationRootNativeBoundary(string kind)
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var define = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var definitionContext = fixture.Context(define);
        var firstDefinition = await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None);
        Assert.True(firstDefinition.IsSuccess, firstDefinition.Error?.Message);
        var generation = new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId, close, "exact-generation-key");
        var generationContext = new Cntryl.Portia.RequestContext<GenerateAuditPopulation>(generation,
            new RequestDispatchContext(definitionContext.Actor, new HttpInvocation("POST", "/test", "/test", "audit-root-test"), RequestMetadata.Create()));
        if (kind == "generate")
        {
            var generated = await new GenerateAuditPopulationHandler(fixture.Mutations, fixture.Clock).HandleAsync(generationContext, CancellationToken.None);
            Assert.True(generated.IsSuccess, generated.Error?.Message);
        }
        var before = await fixture.Reader.HydrateAsync(new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId));
        fixture.AdvanceClock();

        // Act
        var retry = kind == "define"
            ? await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None)
            : await new GenerateAuditPopulationHandler(fixture.Mutations, fixture.Clock).HandleAsync(generationContext, CancellationToken.None);
        var after = await fixture.Reader.HydrateAsync(new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }
}
