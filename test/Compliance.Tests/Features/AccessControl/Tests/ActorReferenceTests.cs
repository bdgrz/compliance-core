using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ActorReferenceTests
{
    [Fact]
    public async Task ShouldPersistNamedSystemProcessGivenReactorEffect()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build();
        _ = services.AddCompliance(configuration);
        using var provider = services.BuildServiceProvider();
        var principals = provider.GetRequiredService<IReactorPrincipalProvider>();
        var reactor = new NamedReactor(new InMemoryProjectionCheckpointStore());
        var actor = principals.GetPrincipal(reactor);
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var context = new RequestContext<DefineTeam>(new DefineTeam(tenantId, teamId,
            "Administrators"), actor);

        // Act
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var result = await new DefineTeamHandler(
                new NativeOperationExecutor(fixture.Repository, fixture.Repository, content), content, TimeProvider.System)
            .HandleAsync(context, CancellationToken.None);
        DomainEventMetadata? metadata = null;
        await foreach (var record in fixture.Store.ReadAsync(
                           new Team(tenantId, teamId).Stream, 0, CancellationToken.None))
            metadata = record.Event.Metadata;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(RequestActor.IsSystem(actor));
        Assert.Equal(new ActorAttribution("reactor:TenantRbacBootstrap", "bdgrz.system"),
            metadata?.Actor);
        Assert.Equal(ActorReference.ForSystemProcess("reactor:TenantRbacBootstrap",
                "TenantRbacBootstrap"),
            ActorReference.FromSystemMetadata(metadata!));
    }

    [Fact]
    public void ShouldLeaveProcessUnknownGivenLegacyGenericSystemActor()
    {
        // Arrange
        var metadata = new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            1, DateTimeOffset.UtcNow)
        {
            Actor = new ActorAttribution("portia:system", "Portia"),
        };

        // Act
        var actor = ActorReference.FromSystemMetadata(metadata);

        // Assert
        Assert.Null(actor);
    }

    sealed class NamedReactor(IProjectionCheckpointStore checkpoints)
        : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"),
            "TenantRbacBootstrap");
}
