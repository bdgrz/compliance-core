using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class ListAdvisorRequestsHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldListHandoffsWithinRequestedProgramGivenCurrentDirectory()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selectedProgram = new AdvisorRequestSummaryView(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), "Readiness review", "ready_for_review", Now);
        var directory = new FixedDirectory(new Page<AdvisorRequestSummaryView>(
            [selectedProgram], "next"));
        var request = new ListAdvisorRequests(fixture.TenantId, fixture.ProgramId,
            Limit: 25, Cursor: "after-id");

        // Act
        var result = await new ListAdvisorRequestsHandler(directory,
                new AdvisorHandoffListReadConsistency(directory, fixture.Events))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([selectedProgram], result.Value.Items);
        Assert.Equal("next", result.Value.NextCursor);
        Assert.Equal(25, directory.LastLimit);
        Assert.Equal("after-id", directory.LastCursor);
    }

    [Fact]
    public async Task ShouldRejectHandoffFromAnotherProgramGivenCorruptProjectionRow()
    {
        // Arrange
        await using var fixture = new Fixture();
        var foreignProgram = new AdvisorRequestSummaryView(fixture.TenantId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Foreign review", "draft", Now);
        var directory = new FixedDirectory(new Page<AdvisorRequestSummaryView>([foreignProgram], null));

        // Act
        var result = await new ListAdvisorRequestsHandler(directory,
                new AdvisorHandoffListReadConsistency(directory, fixture.Events))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(new ListAdvisorRequests(
                fixture.TenantId, fixture.ProgramId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectUnboundedPageGivenInvalidLimit()
    {
        // Arrange
        await using var fixture = new Fixture();
        var directory = new FixedDirectory(new Page<AdvisorRequestSummaryView>([], null));

        // Act
        var result = await new ListAdvisorRequestsHandler(directory,
                new AdvisorHandoffListReadConsistency(directory, fixture.Events))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(new ListAdvisorRequests(
                fixture.TenantId, fixture.ProgramId, Limit: 201), RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task ShouldFailTransientlyGivenPendingHandoffEvents()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedHandoffAsync();
        var directory = new FixedDirectory(new Page<AdvisorRequestSummaryView>([], null));

        // Act
        var result = await new ListAdvisorRequestsHandler(directory,
                new AdvisorHandoffListReadConsistency(directory, fixture.Events))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(new ListAdvisorRequests(
                fixture.TenantId, fixture.ProgramId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(0, directory.Calls);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateWriter _writer;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public IDomainEventReader Events { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        }

        public async Task SeedHandoffAsync()
        {
            var createdAt = Now.AddMinutes(-1);
            var handoff = new AdvisorHandoff(TenantId, ProgramId, Uuid.CreateVersion4());
            Assert.True(handoff.Create("Review", [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())],
                new DateOnly(2026, 12, 31), ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"),
                createdAt).IsSuccess);
            await _writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedDirectory(Page<AdvisorRequestSummaryView> page)
        : IAdvisorHandoffDirectoryReader
    {
        public int Calls { get; private set; }
        public int LastLimit { get; private set; }
        public string? LastCursor { get; private set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AdvisorRequestSummaryView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            Calls++;
            LastLimit = limit;
            LastCursor = cursor;
            return ValueTask.FromResult(page);
        }
    }
}
