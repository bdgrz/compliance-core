using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationVendorImpactTests
{
    [Fact]
    public void ShouldCanonicalizeVendorReferencesGivenImpactDigest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var first = Reference(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var second = Reference(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var preview = new ApplicationChangePreview(tenantId, applicationId, 1, "retire",
            [], [], [], false)
        {
            VendorReferences = [first, second],
        };
        var reordered = preview with { VendorReferences = [second, first] };

        // Act
        var digest = ApplicationChangeImpactDigest.Compute(preview);
        var reorderedDigest = ApplicationChangeImpactDigest.Compute(reordered);

        // Assert
        Assert.Equal(digest, reorderedDigest);
    }

    [Fact]
    public async Task ShouldIncludeCurrentProviderDependenciesGivenApplicationImpact()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var systemInstanceId = Uuid.CreateVersion4();
        await scenario.ProjectSystemInstanceAsync(systemInstanceId, 1);
        var unrelatedApplicationId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var provider = Provider(scenario.TenantId, providerId,
        [
            Dependency(systemInstanceId, scenario.ApplicationId, "Hosts payroll", 1, 1),
            Dependency(Uuid.CreateVersion4(), unrelatedApplicationId,
                "Hosts another service", 2, 1),
        ]);
        var providers = new StubProviderReader(new Page<ProviderView>([provider], null));
        var handler = scenario.Handler(providers);

        // Act
        var result = await scenario.PreviewAsync(handler);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reference = Assert.Single(result.Value.VendorReferences);
        Assert.Equal(providerId, reference.ProviderId);
        Assert.Equal("Payroll provider", reference.ProviderName);
        Assert.Equal("SaaS", reference.ProviderKind);
        Assert.Equal(provider.Revision, reference.ProviderRevision);
        Assert.Equal(0, reference.DependencyIndex);
        Assert.Equal(systemInstanceId, reference.SystemInstanceId);
        Assert.Equal(1, reference.SystemInstanceRevision);
        Assert.Equal(1, reference.ApplicationRevision);
        Assert.Equal("Hosts payroll", reference.Rationale);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value,
            ComplianceCoreJsonContext.Default.ApplicationChangePreview));
        var serializedReference = Assert.Single(json.RootElement
            .GetProperty("vendor_references").EnumerateArray().ToArray());
        Assert.Equal(providerId.ToString(),
            serializedReference.GetProperty("provider_id").GetString());
        Assert.Equal("Hosts payroll",
            serializedReference.GetProperty("rationale").GetString());
        Assert.DoesNotContain("vendors", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldKeepVendorsPendingGivenProviderPageOverflow()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var provider = Provider(scenario.TenantId, Uuid.CreateVersion4(),
        [
            Dependency(Uuid.CreateVersion4(), scenario.ApplicationId, "Hosts payroll", 3, 1),
        ]);
        var providers = new StubProviderReader(new Page<ProviderView>([provider], "next"));
        var handler = scenario.Handler(providers);

        // Act
        var result = await scenario.PreviewAsync(handler);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.VendorReferences);
        Assert.Contains("vendors", result.Value.PendingContexts);
        Assert.Contains("vendor_references_over_limit", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Theory]
    [InlineData("stale_revision")]
    [InlineData("unresolved_dependency")]
    public async Task ShouldKeepVendorsPendingGivenUnverifiableProviderDependency(string kind)
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        ProviderDependency dependency;
        if (kind == "stale_revision")
        {
            var systemInstanceId = Uuid.CreateVersion4();
            await scenario.ProjectSystemInstanceAsync(systemInstanceId, 1);
            dependency = Dependency(systemInstanceId, scenario.ApplicationId,
                "Hosts payroll", 2, 1);
        }
        else
        {
            dependency = UnresolvedSystemDependency("Unmapped payroll system");
        }
        var provider = Provider(scenario.TenantId, Uuid.CreateVersion4(), [dependency]);
        var handler = scenario.Handler(new StubProviderReader(
            new Page<ProviderView>([provider], null)));

        // Act
        var result = await scenario.PreviewAsync(handler);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("vendors", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
        Assert.Equal(kind == "stale_revision" ? 1 : 0,
            result.Value.VendorReferences.Count);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenProviderProjectionBehindSource()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var providerId = Uuid.CreateVersion4();
        var providerEvent = NamedRecordFacts.ProviderRecordedFact(scenario.TenantId, providerId,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), now);
        providerEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            providerId, 1, now));
        await scenario.Events.AppendAsync(new Provider(scenario.TenantId, providerId).Stream, 0, [providerEvent]);
        var providers = new StubProviderReader(new Page<ProviderView>([], null));
        var handler = scenario.Handler(providers);

        // Act
        var result = await scenario.PreviewAsync(handler);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectCrossTenantProviderProjectionGivenApplicationImpact()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var provider = Provider(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        [
            Dependency(Uuid.CreateVersion4(), scenario.ApplicationId, "Hosts payroll", 3, 1),
        ]);
        var providers = new StubProviderReader(new Page<ProviderView>([provider], null));
        var handler = scenario.Handler(providers);

        // Act
        var result = await scenario.PreviewAsync(handler);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    static ProviderView Provider(Uuid tenantId, Uuid providerId,
        IReadOnlyList<ProviderDependency> dependencies)
    {
        var content = ProviderRules.Normalize(new ProviderContent("Payroll provider", "SaaS",
            Dependencies: dependencies));
        return new ProviderView(tenantId, providerId, 2, content, "manual", "active",
            ProviderRules.Unresolved(content),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Provider manager"),
            DateTimeOffset.UtcNow);
    }

    static ProviderDependency Dependency(Uuid systemInstanceId, Uuid applicationId,
        string rationale, long systemInstanceRevision, long applicationRevision) =>
        new("system_instance", systemInstanceId, null, applicationId, rationale,
            DateTimeOffset.UnixEpoch)
        {
            SourceRevision = systemInstanceRevision,
            ApplicationRevision = applicationRevision,
        };

    static ProviderDependency UnresolvedSystemDependency(string rationale) =>
        new("system_instance", null, null, null, rationale, DateTimeOffset.UnixEpoch,
            UnresolvedReference: "Unknown system");

    sealed class Scenario
    {
        readonly Uuid tenantId;
        readonly Uuid applicationId;
        readonly IAggregateReader reader;
        readonly FitzApplicationDirectory applications;
        readonly FitzApplicationBoundaryReferenceDirectory boundaries;
        readonly FitzApplicationControlDraftReferenceDirectory controls;
        readonly InMemoryEventStore events;

        Scenario(Uuid tenantId, Uuid applicationId, IAggregateReader reader,
            FitzApplicationDirectory applications,
            FitzApplicationBoundaryReferenceDirectory boundaries,
            FitzApplicationControlDraftReferenceDirectory controls, InMemoryEventStore events)
        {
            this.tenantId = tenantId;
            this.applicationId = applicationId;
            this.reader = reader;
            this.applications = applications;
            this.boundaries = boundaries;
            this.controls = controls;
            this.events = events;
            TenantId = tenantId;
            ApplicationId = applicationId;
            Events = events;
        }

        public Uuid TenantId { get; }
        public Uuid ApplicationId { get; }
        public InMemoryEventStore Events { get; }

        public static async Task<Scenario> CreateAsync()
        {
            var tenantId = Uuid.CreateVersion4();
            var applicationId = Uuid.CreateVersion4();
            var actorId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            var source = new DeclaredApplication(tenantId, applicationId);
            Assert.True(source.Declare("Payroll", "Payroll processing", null, actorId,
                "Manager", now).IsSuccess);
            var reader = new SourceReader(source);
            var kv = new InMemoryKvClient();
            var applications = new FitzApplicationDirectory(kv);
            await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
                tenantId, applicationId, "Payroll", "Payroll processing", null, actorId,
                "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
            return new Scenario(tenantId, applicationId, reader, applications,
                new FitzApplicationBoundaryReferenceDirectory(kv),
                new FitzApplicationControlDraftReferenceDirectory(kv, new ControlProjectionFacts().Content),
                new InMemoryEventStore());
        }

        public PreviewApplicationChangeHandler Handler(IProviderReader? providers = null)
        {
            var providerConsistency = providers is null
                ? null
                : new ProviderReadConsistency(providers, reader, events);
            var instanceConsistency = providers is null
                ? null
                : new SystemInstanceReadConsistency(applications, reader,
                    new LegacySystemInstanceSource(applications, events), events);
            return new PreviewApplicationChangeHandler(reader, applications, boundaries,
                new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
                new ApplicationControlDraftReferenceReadConsistency(controls, events),
                RestrictedApplicationVisibilityFixture.Create(reader),
                instanceConsistency: instanceConsistency, providers: providers,
                providerConsistency: providerConsistency);
        }

        public Task<Result<ApplicationChangePreview>> PreviewAsync(
            PreviewApplicationChangeHandler handler) =>
            handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
                new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
                new System.Security.Claims.ClaimsPrincipal()), CancellationToken.None).AsTask();

        public Task ProjectSystemInstanceAsync(Uuid systemInstanceId, long revision) =>
            ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
                tenantId, applicationId, systemInstanceId, revision, "Payroll production",
                "production", null, null, Uuid.CreateVersion4(), "Manager",
                DateTimeOffset.UtcNow, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
    }

    sealed class StubProviderReader(Page<ProviderView> page) : IProviderReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<ProviderView?> GetAsync(Uuid tenantId, Uuid providerId,
            CancellationToken ct = default) => ValueTask.FromResult<ProviderView?>(null);

        public ValueTask<ProviderView?> GetRevisionAsync(Uuid tenantId, Uuid providerId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<ProviderView?>(null);

        public ValueTask<Page<ProviderView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(page);

        public ValueTask<Page<ProviderView>> ListRevisionsAsync(Uuid tenantId, Uuid providerId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProviderView>([], null));
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory,
        Uuid tenantId, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(tenantId.ToString())),
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static ApplicationVendorReferenceView Reference(Uuid providerId, Uuid systemInstanceId) =>
        new(providerId, "Payroll provider", "SaaS", 2, "material", true, "inclusive",
            0, systemInstanceId, 3, 4, "Hosts payroll", DateTimeOffset.UnixEpoch, null);
}
