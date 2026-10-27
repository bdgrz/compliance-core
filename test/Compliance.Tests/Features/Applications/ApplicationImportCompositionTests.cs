using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Projectors;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreviewRetainedSourceOmissionGivenProductionCompositionAndCurrentImportAuthority(bool stageOnly)
    {
        // Arrange
        await using var fixture = new Fixture();
        var (batchId, originalBatchId, applicationId) = await fixture.SeedAsync();
        fixture.Permissions.StageOnly = stageOnly;

        // Act
        var result = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, batchId), fixture.Actor);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, applicationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var missing = Assert.Single(result.Value!.Items);
        Assert.Equal("omitted", missing.SourceRecordId);
        Assert.Equal(originalBatchId, missing.LastObservedBatchId);
        Assert.Equal(applicationId, missing.ApplicationId);
        Assert.Equal("missing_from_source", missing.MatchState);
        Assert.Contains("missing_source_retirement_unavailable", missing.AcceptanceBlockers);
        Assert.Contains("retirement_impact_unavailable", missing.AcceptanceBlockers);
        Assert.True(target.IsCreated);
        Assert.False(target.IsRetired);
        Assert.Equal(1, target.Revision);
    }

    [Theory]
    [InlineData("nonmember", RequestErrorKind.NotFound)]
    [InlineData("foreign_tenant", RequestErrorKind.NotFound)]
    [InlineData("suspended", RequestErrorKind.NotFound)]
    [InlineData("deprovisioned", RequestErrorKind.NotFound)]
    [InlineData("firm_staff", RequestErrorKind.Forbidden)]
    [InlineData("missing_grant", RequestErrorKind.Forbidden)]
    public async Task ShouldDenyMissingPreviewGivenCurrentOwningImportAuthorityLost(string denial, RequestErrorKind expected)
    {
        // Arrange
        await using var fixture = new Fixture();
        var (batchId, _, _) = await fixture.SeedAsync();
        fixture.Memberships.State = denial;
        fixture.Permissions.Allowed = denial != "missing_grant";
        var tenant = denial == "foreign_tenant" ? Uuid.CreateVersion4() : fixture.Tenant;

        // Act
        var result = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(tenant, batchId), fixture.Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error?.Kind);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor => new(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));
        public Permissions Permissions { get; } = new();
        public MembershipDirectory Memberships { get; }
        public IServiceProvider Provider => _provider;
        public IRequestBus Bus { get; }
        public IAggregateReader Reader { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build();
            services.AddCompliance(configuration, developerAuthentication: true);
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IAuthoritativeSnapshotReader>(new EventStoreSnapshotReader(store));
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            Memberships = new MembershipDirectory(Tenant, User);
            services.AddSingleton<ITenantMembershipDirectoryReader>(Memberships);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public IImmutableContentStore Content => _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();

        /// <summary>Sends a personal HTTP 202 command, as the accepted routes do.</summary>
        public ValueTask<Result<Bdgrz.Compliance.Features.NativeOperations.Models.OperationAcceptedResponse>> SendPersonalAcceptedAsync(
            IRequest<Bdgrz.Compliance.Features.NativeOperations.Models.OperationAcceptedResponse> request) =>
            Bus.DispatchAsync(request, new RequestDispatchContext(Actor, new HttpInvocation("PUT", "/synthetic/import",
                "/synthetic/import", "synthetic")), CancellationToken.None);

        /// <summary>The staged rows of a batch, from its immutable rows manifest.</summary>
        public async Task<IReadOnlyList<ApplicationImportStagedRow>> RowsOfAsync(Uuid batchId) =>
            (await ApplicationImportContent.ReadAsync<ApplicationImportRowsManifest>(Content, Tenant,
                (await Reader.HydrateAsync(new ApplicationImportBatch(Tenant, batchId))).Staged!.RowsManifest)).Rows;

        public ValueTask<Result> SendPersonalAsync(IRequest request) => Bus.DispatchAsync(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/synthetic/import", "/synthetic/import", "synthetic")), CancellationToken.None);

        public async Task<(Uuid BatchId, Uuid OriginalBatchId, Uuid ApplicationId)> SeedAsync(string coverage = "declared_complete", bool invalid = false)
        {
            var original = await Bus.SendAsync(new StageApplicationImport(Tenant, Uuid.CreateVersion4(),
                "manual", "applications", "partial", [new("omitted", "Observed application", "Purpose", null)]), Actor);
            Assert.True(original.IsSuccess, original.Error?.Message);
            var originalBatch = await Reader.HydrateAsync(new ApplicationImportBatch(Tenant, original.Value!.BatchId));
            var rows = await ApplicationImportContent.ReadAsync<ApplicationImportRowsManifest>(
                _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), Tenant,
                originalBatch.Staged!.RowsManifest, CancellationToken.None);
            var row = Assert.Single(rows.Rows);
            Assert.True((await SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(), Tenant,
                originalBatch.Id, row.RowId, 1, "create_new", null, null, "New source identity"))).IsSuccess);
            var accepted = await Bus.DispatchAsync(new AcceptApplicationImport(Uuid.CreateVersion4(), Tenant,
                originalBatch.Id, 2), new RequestDispatchContext(Actor, new HttpInvocation("PUT", "/synthetic/import",
                "/synthetic/import", "synthetic")), CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            var run = await ApplicationImportProcessSupport.RunAsync(_provider, _scope.ServiceProvider, Tenant,
                originalBatch.Id);
            Assert.Equal(Bdgrz.Compliance.Features.Processes.ProcessState.Completed, run.State);
            var published = await Reader.HydrateAsync(new ApplicationImportBatch(Tenant, originalBatch.Id));
            Assert.Equal("committed", published.State);
            var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(
                _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), Tenant,
                published.Plan!.FrozenPlan, CancellationToken.None);
            var applicationId = Assert.Single(await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(
                _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), Tenant, plan.Rows)).ApplicationId;
            await CatchUpApplicationDirectoryAsync();
            var staged = await Bus.SendAsync(new StageApplicationImport(Tenant, Uuid.CreateVersion4(),
                "manual", "applications", coverage, [new("present", invalid ? "" : "Present application", "Purpose", null)]), Actor);
            Assert.True(staged.IsSuccess, staged.Error?.Message);
            await CatchUpImportDirectoryAsync();
            return (staged.Value!.BatchId, originalBatch.Id, applicationId);
        }

        ProjectionCheckpoint _importCheckpoint = ProjectionCheckpoint.Start;

        /// <summary>Projects every committed import fact into the import directory, resuming from its checkpoint.</summary>
        public async Task CatchUpImportDirectoryAsync()
        {
            var services = _scope.ServiceProvider;
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(),
                candidate => candidate.Name == "ApplicationImportDirectoryV1");
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(Tenant.ToString())).RunAsync(projector);
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            while (true)
            {
                var next = await runner.RunAsync(projector, _importCheckpoint);
                if (next == _importCheckpoint)
                    break;
                _importCheckpoint = next;
            }
        }

        async Task CatchUpApplicationDirectoryAsync()
        {
            var services = _scope.ServiceProvider;
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(),
                candidate => candidate.Name == "ApplicationDirectoryV2");
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(Tenant.ToString())).RunAsync(projector);
            var directory = services.GetRequiredService<IApplicationDirectoryReader>();
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            var checkpoint = await directory.LoadCheckpointAsync(Tenant);
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }

            var incomingRegistration = Assert.Single(services.GetServices<WorkloadRegistration>(),
                candidate => candidate.Name == ApplicationIncomingRelationshipProjector.ProjectorName);
            var incomingProjector = (Projector)services.GetRequiredService(
                incomingRegistration.ComponentType);
            await new ProjectorScenario(new TenantId(Tenant.ToString()))
                .RunAsync(incomingProjector);
            var incomingDirectory = services.GetRequiredService<
                IApplicationIncomingRelationshipDirectory>();
            var incomingRunner = new ProjectorRunner(
                services.GetRequiredService<IDomainEventReader>());
            var incomingCheckpoint = await incomingDirectory.LoadCheckpointAsync(Tenant);
            while (true)
            {
                var next = await incomingRunner.RunAsync(incomingProjector, incomingCheckpoint);
                if (next == incomingCheckpoint)
                    break;
                incomingCheckpoint = next;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    internal sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public bool StageOnly { get; set; }
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed &&
            (permission == RbacPermissions.ApplicationImportStage ||
             !StageOnly && permission == RbacPermissions.ApplicationInventoryManage));
    }

    internal sealed class MembershipDirectory(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public string State { get; set; } = "active";
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(State != "nonmember" && tenantId == tenant.ToString() && userId == user
                ? new TenantMembershipView(userId, tenant, State == "firm_staff" ? "firm_staff" : "client_personnel",
                    IsSuspended: State == "suspended", IsDeprovisioned: State == "deprovisioned") : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            await GetAsync(tenantId, userId, ct) is { IsSuspended: false, IsDeprovisioned: false };
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
