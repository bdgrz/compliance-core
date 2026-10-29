using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class RestrictedAccessPopulationReadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRespectSystemInstanceGrantGivenPopulationPreview(bool hasGrant)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, memberId,
            "Manager", now, isRestricted: true).IsSuccess);
        var population = new AccessPopulation(tenantId, populationId);
        Assert.True(population.Open(applicationId, instanceId, 1, now, "Provider export",
            ActorReference.ForMember(memberId, "Manager"), now).IsSuccess);
        var reader = new SourceReader(application, population);
        var visibility = RestrictedApplicationVisibilityFixture.Create(reader,
            allowedScopes: hasGrant
                ? [new AccessGrantScope(AccessGrantScopeKind.SystemInstance, instanceId)]
                : []);
        var handler = new PreviewAccessPopulationHandler(reader, AccessReviewFacts.Content(), visibility);

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewAccessPopulation>(
            new PreviewAccessPopulation(tenantId, populationId), Actor(userId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(hasGrant, result.IsSuccess);
        if (!hasGrant)
            Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRespectSystemInstanceGrantGivenRestrictedPopulation(bool hasGrant)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, memberId,
            "Manager", now, isRestricted: true).IsSuccess);
        var population = new AccessPopulation(tenantId, populationId);
        Assert.True(population.Open(applicationId, instanceId, 1, now, "Provider export",
            ActorReference.ForMember(memberId, "Manager"), now).IsSuccess);
        var reader = new SourceReader(application, population);
        var visibility = RestrictedApplicationVisibilityFixture.Create(reader,
            allowedScopes: hasGrant
                ? [new AccessGrantScope(AccessGrantScopeKind.SystemInstance, instanceId)]
                : []);
        var handler = new GetAccessPopulationHandler(reader, AccessReviewFacts.Content(), visibility);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetAccessPopulation>(
            new GetAccessPopulation(tenantId, populationId), Actor(userId)),
            CancellationToken.None);

        // Assert
        if (hasGrant)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(instanceId, result.Value.SystemInstanceId);
        }
        else
            Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHidePopulationWriteGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, memberId,
            "Manager", now, isRestricted: true).IsSuccess);
        var population = new AccessPopulation(tenantId, populationId);
        Assert.True(population.Open(applicationId, instanceId, 1, now, "Provider export",
            ActorReference.ForMember(memberId, "Manager"), now).IsSuccess);
        var reader = new SourceReader(application, population);
        var handler = new RecordAccessPopulationFactsHandler(null!, reader, AccessReviewFacts.Content(),
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(reader));
        var context = new RequestContext<RecordAccessPopulationFacts>(
            new RecordAccessPopulationFacts(Uuid.CreateVersion4(), tenantId, populationId, 1, [], [], [], []),
            Actor(userId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHidePopulationAcceptanceGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, memberId,
            "Manager", now, isRestricted: true).IsSuccess);
        var population = new AccessPopulation(tenantId, populationId);
        Assert.True(population.Open(applicationId, instanceId, 1, now, "Provider export",
            ActorReference.ForMember(memberId, "Manager"), now).IsSuccess);
        var reader = new SourceReader(application, population);
        var handler = new AcceptAccessPopulationHandler(null!, reader, null!, AccessReviewFacts.Content(),
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(reader));
        var context = PersonalAccessReviewTransportTests.HttpContext<AcceptAccessPopulation>(
            new AcceptAccessPopulation(Uuid.CreateVersion4(), tenantId, populationId, 1,
                "I attest to this data."),
            Actor(userId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

    sealed class SourceReader(DeclaredApplication application, AccessPopulation population)
        : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate switch
            {
                DeclaredApplication => (TAggregate)(Aggregate)application,
                AccessPopulation => (TAggregate)(Aggregate)population,
                _ => aggregate,
            });
    }
}
