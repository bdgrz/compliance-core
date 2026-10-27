using System.Security.Claims;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
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
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Handlers;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

/// <summary>Behavioral RED cases compile against both the combined and the selected split command surfaces.</summary>
public sealed class ArtifactRetentionSplitRegressionTests
{
    [Fact]
    public async Task ShouldConflictGivenCreationRetryWithChangedOriginalOwnerRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifact = await fixture.EvidenceAsync();
        var request = new PlaceArtifactLegalHold(fixture.Tenant, "evidence_artifact", artifact, Fixture.Sha,
            Uuid.CreateVersion4(), 0, "Preserve exact source");
        var handler = new PlaceArtifactLegalHoldHandler(fixture.Mutation);
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);

        // Act
        var changed = await handler.HandleAsync(fixture.Context(request with { ExpectedRevision = 1 }), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAdmitBothHoldsGivenDifferentHoldIdentitiesAndInitialOwnerRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifact = await fixture.EvidenceAsync();
        var first = new PlaceArtifactLegalHold(fixture.Tenant, "evidence_artifact", artifact, Fixture.Sha,
            Uuid.CreateVersion4(), 0, "Matter A");
        var second = first with { HoldId = Uuid.CreateVersion4(), Reason = "Matter B" };
        var handler = new PlaceArtifactLegalHoldHandler(fixture.Mutation);

        // Act
        var results = await Task.WhenAll(handler.HandleAsync(fixture.Context(first), CancellationToken.None).AsTask(),
            handler.HandleAsync(fixture.Context(second), CancellationToken.None).AsTask());

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
    }

    sealed class Fixture : IAsyncDisposable
    {
        public const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateWriter _writer;
        readonly ClaimsPrincipal _actor = new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public ArtifactRetentionMutation Mutation { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new InMemoryKvClient()));
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<NativeOperationExecutor>();
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Mutation = ActivatorUtilities.CreateInstance<ArtifactRetentionMutation>(_scope.ServiceProvider);
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(_actor, new HttpInvocation("PUT", "/synthetic/retention", null, "retention-red")));

        public async Task<Uuid> EvidenceAsync()
        {
            var id = Uuid.CreateVersion4();
            var artifact = new EvidenceArtifact(Tenant, id);
            Assert.True(artifact.Register(new("Evidence", null, "export", "manual", DateTimeOffset.UtcNow,
                    new(2019, 1, 1), new(2019, 12, 31), "confidential"), Sha, 100,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Collector"), DateTimeOffset.UtcNow).IsSuccess);
            await _writer.SaveAsync(artifact, Context(new GetArtifactRetention(Tenant, "evidence_artifact", id)));
            return id;
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
