using Bdgrz.Compliance.Tests.Hosting.Persistence;
using System.Security.Claims;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.History;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.AuditPopulations.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;

public sealed partial class DefineAuditPopulationHandlerTests
{
    [Theory]
    [InlineData("define", true)]
    [InlineData("define", false)]
    [InlineData("generate", true)]
    [InlineData("generate", false)]
    public async Task ShouldRejectUnavailableOrCorruptSourceGivenOriginalRootRetry(string kind, bool missing)
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var define = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var definitionContext = fixture.Context(define);
        Assert.True((await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None)).IsSuccess);
        var generate = new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            close, "source-verified-generation");
        var generationContext = new Cntryl.Portia.RequestContext<GenerateAuditPopulation>(generate,
            new RequestDispatchContext(definitionContext.Actor, new HttpInvocation("POST", "/test", "/test", "audit-root-test"), RequestMetadata.Create()));
        if (kind == "generate")
            Assert.True((await new GenerateAuditPopulationHandler(fixture.Mutations, fixture.Clock)
                .HandleAsync(generationContext, CancellationToken.None)).IsSuccess);
        var before = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        var fact = Assert.IsAssignableFrom<IAuditPopulationNativeFact>(before.Facts[^1]);
        var fault = new PopulationReadFault(fixture.PeriodContent, fact.SourceProofs, missing, null);

        // Act
        var result = kind == "define"
            ? await new DefineAuditPopulationHandler(fixture.MutationsFor(fault), fixture.Periods, fixture.Clock)
                .HandleAsync(definitionContext, CancellationToken.None)
            : await new GenerateAuditPopulationHandler(fixture.MutationsFor(fault), fixture.Clock)
                .HandleAsync(generationContext, CancellationToken.None);
        var after = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.True(fault.Reads > 0);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(missing, result.Error?.IsTransient);
        Assert.Equal(before.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldCommitOneDefinitionGivenConcurrentIdenticalRequests()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var handler = fixture.Handler();

        // Act
        var results = await Task.WhenAll(handler.HandleAsync(fixture.Context(request), CancellationToken.None).AsTask(),
            handler.HandleAsync(fixture.Context(request), CancellationToken.None).AsTask());
        var facts = new List<DomainEvent>();
        var stream = new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId).Stream;
        await foreach (var record in fixture.PeriodEvents.ReadAsync(stream, 0, CancellationToken.None))
            facts.Add(record.Event);

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        var defined = Assert.Single(facts.OfType<AuditPopulationDefinedV4>());
        await using var persistence = EventPersistenceFrameTests.Compose();
        var serializer = persistence.GetRequiredService<IDomainEventSerializer>();
        Assert.Equal(defined, serializer.Deserialize(serializer.Serialize(defined)));
    }

    [Fact]
    public async Task ShouldReplayExistingV3DefinitionGivenReceiptBearingDefinitionSupport()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var context = fixture.Context(request);
        Assert.True(AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, fixture.TenantId,
            out var memberId, out var display));
        var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var accepted = await fixture.Mutations.AppendUnqualifiedAsync(context, fixture.TenantId, fixture.ProgramId,
            fixture.PopulationId, rules => rules.Define(request.DefinitionId, request.Definition, period.View!,
                memberId, display, fixture.Clock.GetUtcNow()), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);

        // Act
        var replay = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.Contains(replay.Facts, fact => fact is AuditPopulationDefinedV3);
        Assert.Equal(request.DefinitionId, replay.View!.AuditPopulationDefinitionId);
    }

    [Fact]
    public async Task ShouldRejectChangedDefinitionGivenPreviouslyAcceptedDefinitionId()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var before = await fixture.Reader.HydrateAsync(new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Act
        var changed = request with { Definition = request.Definition with { SourceUniverse = "A different source" } };
        var result = await fixture.Handler().HandleAsync(fixture.Context(changed), CancellationToken.None);
        var after = await fixture.Reader.HydrateAsync(new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRejectDifferentActorGivenPreviouslyAcceptedDefinitionId()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var other = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString()),
                new Claim("email", "other@example.test")], "BdgrzSession"));

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request, other), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectDifferentResultGivenFrozenGenerationResponse()
    {
        // Arrange
        await using var fixture = new Fixture();
        var original = await NativeContent.FreezeUnitAsync(fixture.PeriodContent, fixture.TenantId);
        var other = await NativeContent.FreezeUnitAsync(fixture.PeriodContent, fixture.TenantId);
        var first = await FirstResponses.FreezeAsync(fixture.PeriodContent, fixture.TenantId.ToString(), original.Response);
        await AuditPopulationHistory.VerifyUnitReceiptAsync(fixture.PeriodContent, fixture.TenantId,
            first, original.Result, CancellationToken.None);

        // Act
        var failure = await Record.ExceptionAsync(() => AuditPopulationHistory.VerifyUnitReceiptAsync(
            fixture.PeriodContent, fixture.TenantId, first, other.Result, CancellationToken.None));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldReplayExistingV3GenerationGivenReceiptBearingGenerationSupport()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var define = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var definitionContext = fixture.Context(define);
        Assert.True((await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None)).IsSuccess);
        var request = new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            close, "legacy-generation");
        var context = new Cntryl.Portia.RequestContext<GenerateAuditPopulation>(request,
            new RequestDispatchContext(definitionContext.Actor, new HttpInvocation("POST", "/test", "/test", "audit-root-test"), RequestMetadata.Create()));
        Assert.True(AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, fixture.TenantId,
            out var memberId, out var display));
        var original = await fixture.Mutations.AppendUnqualifiedAsync(context, fixture.TenantId, fixture.ProgramId,
            fixture.PopulationId, rules => rules.Generate(rules.Revision, request.GenerationKey,
                memberId, display, fixture.Clock.GetUtcNow()), CancellationToken.None);
        Assert.True(original.IsSuccess, original.Error?.Message);

        // Act
        var replay = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.Equal("legacy-generation", replay.View!.GenerationKey);
        Assert.Contains(replay.Facts, fact => fact is AuditPopulationGeneratedV3);
    }

    [Fact]
    public async Task ShouldCommitOneGenerationGivenConcurrentIdenticalRequests()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var define = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var definitionContext = fixture.Context(define);
        Assert.True((await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None)).IsSuccess);
        var request = new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            close, "concurrent-generation");
        Cntryl.Portia.RequestContext<GenerateAuditPopulation> Context() => new(request,
            new RequestDispatchContext(definitionContext.Actor, new HttpInvocation("POST", "/test", "/test", "audit-root-test"), RequestMetadata.Create()));
        var handler = new GenerateAuditPopulationHandler(fixture.Mutations, fixture.Clock);

        // Act
        var results = await Task.WhenAll(handler.HandleAsync(Context(), CancellationToken.None).AsTask(),
            handler.HandleAsync(Context(), CancellationToken.None).AsTask());
        var facts = new List<DomainEvent>();
        var stream = new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId).Stream;
        await foreach (var record in fixture.PeriodEvents.ReadAsync(stream, 0, CancellationToken.None))
            facts.Add(record.Event);

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        var generated = Assert.Single(facts.OfType<AuditPopulationGeneratedV4>());
        await using var persistence = EventPersistenceFrameTests.Compose();
        var serializer = persistence.GetRequiredService<IDomainEventSerializer>();
        Assert.Equal(generated, serializer.Deserialize(serializer.Serialize(generated)));
    }

    [Fact]
    public async Task ShouldRejectDifferentActorGivenPreviouslyAcceptedGenerationKey()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var define = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var definitionContext = fixture.Context(define);
        Assert.True((await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None)).IsSuccess);
        var request = new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            close, "actor-bound-generation");
        Cntryl.Portia.RequestContext<GenerateAuditPopulation> Context(ClaimsPrincipal actor) => new(request,
            new RequestDispatchContext(actor, new HttpInvocation("POST", "/test", "/test", "audit-root-test"), RequestMetadata.Create()));
        var handler = new GenerateAuditPopulationHandler(fixture.Mutations, fixture.Clock);
        Assert.True((await handler.HandleAsync(Context(definitionContext.Actor), CancellationToken.None)).IsSuccess);
        var other = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString()),
                new Claim("email", "other@example.test")], "BdgrzSession"));

        // Act
        var result = await handler.HandleAsync(Context(other), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectChangedCloseGivenPreviouslyAcceptedGenerationKey()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        var define = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            fixture.PeriodId, close, fixture.Definition());
        var definitionContext = fixture.Context(define);
        Assert.True((await fixture.Handler().HandleAsync(definitionContext, CancellationToken.None)).IsSuccess);
        var request = new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
            close, "generation-key");
        Cntryl.Portia.RequestContext<GenerateAuditPopulation> Context(GenerateAuditPopulation value) => new(value,
            new RequestDispatchContext(definitionContext.Actor, new HttpInvocation("POST", "/test", "/test", "audit-root-test"), RequestMetadata.Create()));
        var handler = new GenerateAuditPopulationHandler(fixture.Mutations, fixture.Clock);
        var first = await handler.HandleAsync(Context(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await fixture.Reader.HydrateAsync(new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Act
        var result = await handler.HandleAsync(Context(request with { PeriodCloseId = Uuid.CreateVersion4() }), CancellationToken.None);
        var after = await fixture.Reader.HydrateAsync(new AuditPopulation(fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

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
