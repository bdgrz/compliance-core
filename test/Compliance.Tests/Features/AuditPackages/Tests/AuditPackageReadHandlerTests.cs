using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.History;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Handlers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

public sealed class AuditPackageReadHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReadNativePackageGivenExactTenantProgramAndPackage()
    {
        // Arrange
        await using var fixture = new Fixture();
        var package = await fixture.SeedAsync("Exact package", Now);

        // Act
        var result = await fixture.GetAsync(package.Id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.Tenant, result.Value!.TenantId);
        Assert.Equal(fixture.Program, result.Value.ProgramId);
        Assert.Equal(package.Id, result.Value.AuditPackageId);
        Assert.Equal("Exact package", result.Value.Name);
        Assert.Equal("draft", result.Value.Status);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("package")]
    public async Task ShouldHidePackageGivenAnotherTenantProgramOrPackage(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        var package = await fixture.SeedAsync("Private package", Now);
        var fault = new FaultedContent(fixture.Content, unavailable: false);
        var handler = new GetAuditPackageHandler(new AuditPackageReader(fixture.Reader, fault, fixture.Events));
        var request = new GetAuditPackage(changed == "tenant" ? Uuid.CreateVersion4() : fixture.Tenant,
            changed == "program" ? Uuid.CreateVersion4() : fixture.Program,
            changed == "package" ? Uuid.CreateVersion4() : package.Id);

        // Act
        var result = await handler.HandleAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, fault.Reads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnTypedContentFailureGivenUnreadableNativePackage(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var package = await fixture.SeedAsync("Exact package", Now);
        var handler = new GetAuditPackageHandler(new AuditPackageReader(fixture.Reader,
            new FaultedContent(fixture.Content, unavailable), fixture.Events));

        // Act
        var result = await handler.HandleAsync(Context(new GetAuditPackage(fixture.Tenant, fixture.Program,
            package.Id)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnCurrentLifecycleGivenDirectoryContainsOnlyCreationIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var package = await fixture.SeedAsync("Current package", Now);
        await fixture.ProjectAsync();
        Assert.Null(package.RequestBuild(package.Revision, "build-1", fixture.Member,
            "Package author", Now.AddMinutes(1)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.Events, fixture.Content, package.Current!);
        await fixture.ProjectAsync();
        var identity = Assert.Single((await fixture.Directory.ListProgramAsync(fixture.Tenant, fixture.Program,
            50, null)).Items);

        // Act
        var result = await fixture.ListAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(string.Empty, identity.Name);
        Assert.Equal("created_identity", identity.Status);
        Assert.Equal(Now, identity.UpdatedAt);
        var summary = Assert.Single(result.Value!.Items);
        Assert.Equal("Current package", summary.Name);
        Assert.Equal("building", summary.Status);
        Assert.Equal(Now.AddMinutes(1), summary.UpdatedAt);
    }

    [Fact]
    public async Task ShouldPageByStableCreationGivenDifferentCurrentUpdateTimesAndForeignScopes()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.SeedAsync("First", Now);
        var second = await fixture.SeedAsync("Second", Now.AddMinutes(1));
        var third = await fixture.SeedAsync("Third", Now.AddMinutes(2));
        await fixture.SeedAsync("Other program", Now, program: Uuid.CreateVersion4());
        var otherTenant = Uuid.CreateVersion4();
        await fixture.SeedAsync("Other tenant", Now, tenant: otherTenant);
        Assert.Null(first.RequestBuild(first.Revision, "later-build", fixture.Member,
            "Package author", Now.AddDays(1)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.Events, fixture.Content, first.Current!);
        await fixture.ProjectAsync();
        await fixture.ProjectAsync(otherTenant);

        // Act
        var page1 = await fixture.ListAsync(limit: 2);
        var page2 = await fixture.ListAsync(limit: 2, cursor: page1.Value!.NextCursor);

        // Assert
        Assert.True(page1.IsSuccess, page1.Error?.Message);
        Assert.True(page2.IsSuccess, page2.Error?.Message);
        Assert.Equal(new[] { first.Id, second.Id }, page1.Value!.Items.Select(value => value.AuditPackageId));
        Assert.Equal("building", page1.Value.Items[0].Status);
        Assert.NotNull(page1.Value.NextCursor);
        Assert.Equal(third.Id, Assert.Single(page2.Value!.Items).AuditPackageId);
        Assert.Null(page2.Value.NextCursor);
        Assert.All(page1.Value.Items.Concat(page2.Value.Items), value =>
        {
            Assert.Equal(fixture.Tenant, value.TenantId);
            Assert.Equal(fixture.Program, value.ProgramId);
        });
    }

    [Fact]
    public async Task ShouldRejectCursorGivenAnotherProgramQuery()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync("First", Now);
        await fixture.SeedAsync("Second", Now.AddMinutes(1));
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedAsync("Other program", Now, program: otherProgram);
        await fixture.ProjectAsync();
        var first = await fixture.ListAsync(limit: 1);
        Assert.NotNull(first.Value!.NextCursor);

        // Act
        var result = await fixture.ListAsync(limit: 1, cursor: first.Value.NextCursor, program: otherProgram);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenAnotherTenantQuery()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync("First", Now);
        await fixture.SeedAsync("Second", Now.AddMinutes(1));
        var otherTenant = Uuid.CreateVersion4();
        await fixture.SeedAsync("Other tenant", Now, tenant: otherTenant);
        await fixture.ProjectAsync();
        await fixture.ProjectAsync(otherTenant);
        var first = await fixture.ListAsync(limit: 1);
        Assert.NotNull(first.Value!.NextCursor);

        // Act
        var result = await fixture.ListAsync(limit: 1, cursor: first.Value.NextCursor, tenant: otherTenant);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(201, null)]
    [InlineData(50, "not-a-cursor")]
    public async Task ShouldReturnValidationGivenInvalidPagination(int limit, string? cursor)
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var result = await fixture.ListAsync(limit, cursor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCreationNotYetProjected()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync("Not yet projected", Now);

        // Act
        var result = await fixture.ListAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnNoPartialPageGivenOnePackageContentCannotBeVerified(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync("Readable first", Now);
        var second = await fixture.SeedAsync("Unreadable second", Now.AddMinutes(1));
        await fixture.ProjectAsync();
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.Tenant, second.Id));
        var failedContent = Assert.IsType<AuditPackageCreated>(Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.Events, owner)).Facts)).Change.ContentId;
        var packages = new AuditPackageReader(fixture.Reader, new FaultedContent(fixture.Content, unavailable,
            failedContent), fixture.Events);
        var handler = new ListAuditPackagesHandler(fixture.Directory,
            new AuditPackageDirectoryReadConsistency(fixture.Directory, fixture.Events), packages);

        // Act
        var result = await handler.HandleAsync(Context(new ListAuditPackages(fixture.Tenant, fixture.Program)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetryWholePageGivenNewSourceArrivesDuringPageRead(bool projectNewSource)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync("First", Now);
        await fixture.ProjectAsync();
        var hooked = new HookedDirectory(fixture.Directory, async () =>
        {
            await fixture.SeedAsync("Arrived during read", Now.AddMinutes(1));
            if (projectNewSource)
                await fixture.ProjectAsync();
        });
        var handler = new ListAuditPackagesHandler(hooked,
            new AuditPackageDirectoryReadConsistency(hooked, fixture.Events),
            new AuditPackageReader(fixture.Reader, fixture.Content, fixture.Events));

        // Act
        var result = await handler.HandleAsync(Context(new ListAuditPackages(fixture.Tenant, fixture.Program)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldKeepCreationIdentityGivenExactProjectionReplayAndRejectChangedProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        var package = await fixture.SeedAsync("Exact package", Now);
        await fixture.ProjectAsync();
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.Tenant, package.Id));
        var created = Assert.IsType<AuditPackageCreated>(Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.Events, owner)).Facts));
        var checkpoint = await fixture.Directory.LoadCheckpointAsync(fixture.Tenant);
        string rejection;

        // Act
        await using (var batch = await fixture.Directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzAuditPackageDirectory.ProjectorName,
                             EventStreamPattern.ForPattern(fixture.Tenant.ToString(), AuditPackageManifest.Area)), checkpoint)))
        {
            await fixture.Directory.ApplyAsync(created);
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.Directory.ApplyAsync(created with { ProgramId = Uuid.CreateVersion4() }).AsTask());
            rejection = changed.Message;
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        Assert.Contains("cannot change", rejection, StringComparison.Ordinal);
        var row = Assert.Single((await fixture.Directory.ListProgramAsync(fixture.Tenant, fixture.Program,
            50, null)).Items);
        Assert.Equal(package.Id, row.AuditPackageId);
    }

    static Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new RequestDispatchContext(RequestActor.System));

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore Events { get; } = new();
        public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient()));
        public FitzAuditPackageDirectory Directory { get; } = new(new InMemoryKvClient());
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }

        public async Task<AuditPackageRules> SeedAsync(string name, DateTimeOffset createdAt,
            Uuid? tenant = null, Uuid? program = null)
        {
            var package = new AuditPackageRules(tenant ?? Tenant, program ?? Program, Uuid.CreateVersion4());
            var content = new AuditPackageContent(Uuid.CreateVersion4(), "type_i", "type_i_baseline",
                Uuid.CreateVersion4(), new string('a', 64), name,
                [new AuditPackageSelectionEntry("system_description", "source", "system_description",
                    Uuid.CreateVersion4(), 1, "snapshot", new string('b', 64), 100, "management",
                    Uuid.CreateVersion4())]);
            Assert.Null(package.Create(content, Member, "Package author", createdAt));
            await PackageSeeds.WriteAsync(Reader, Writer, Events, Content, package.Current!);
            return package;
        }

        public Task<Result<AuditPackageView>> GetAsync(Uuid packageId) => new GetAuditPackageHandler(
            new AuditPackageReader(Reader, Content, Events)).HandleAsync(Context(new GetAuditPackage(Tenant, Program,
                packageId)), CancellationToken.None).AsTask();

        public Task<Result<Page<AuditPackageSummaryView>>> ListAsync(int? limit = null, string? cursor = null,
            Uuid? tenant = null, Uuid? program = null) => new ListAuditPackagesHandler(Directory,
            new AuditPackageDirectoryReadConsistency(Directory, Events), new AuditPackageReader(Reader, Content, Events))
            .HandleAsync(Context(new ListAuditPackages(tenant ?? Tenant, program ?? Program, limit, cursor)),
                CancellationToken.None).AsTask();

        public async Task ProjectAsync(Uuid? tenantId = null)
        {
            var tenant = tenantId ?? Tenant;
            var checkpoint = await Directory.LoadCheckpointAsync(tenant);
            var pattern = EventStreamPattern.ForPattern(tenant.ToString(), AuditPackageManifest.Area);
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(FitzAuditPackageDirectory.ProjectorName, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                if (record.Event is AuditPackageCreated created)
                    await Directory.ApplyAsync(created);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FaultedContent(IImmutableContentStore inner, bool unavailable, Uuid? failedContent = null)
        : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            Reads++;
            return failedContent is { } id && reference.ContentId != id
                ? inner.ReadVerifiedAsync(realm, reference, ct) : throw (unavailable
                    ? new ImmutableContentUnavailableException("The package content is temporarily absent.")
                    : new ImmutableContentIntegrityException("The package content digest differs."));
        }
    }

    sealed class HookedDirectory(IAuditPackageDirectoryReader inner, Func<Task> afterPage)
        : IAuditPackageDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
            inner.LoadCheckpointAsync(tenantId, ct);
        public async ValueTask<Page<AuditPackageSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var page = await inner.ListProgramAsync(tenantId, programId, limit, cursor, ct);
            await afterPage();
            return page;
        }
    }
}
