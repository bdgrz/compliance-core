using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

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
                new AdvisorHandoffListReadConsistency(directory, fixture.Events),
                new FixedListAccess(AdvisorHandoffListVisibility.Full), new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(request,
                    ProgramManagementServices.Actor(Uuid.CreateVersion4())),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([selectedProgram], result.Value.Items);
        Assert.Equal("next", result.Value.NextCursor);
        Assert.Equal(25, directory.LastLimit);
        Assert.Equal("after-id", directory.LastCursor);
    }

    [Fact]
    public async Task ShouldListOnlySharedHandoffsForAdvisorGivenPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var shared = new AdvisorRequestSummaryView(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), "Shared review", "ready_for_review", Now);
        var hiddenDraft = new AdvisorRequestSummaryView(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), "Unshared draft", "draft", Now);
        var directory = new FixedDirectory(new Page<AdvisorRequestSummaryView>(
            [hiddenDraft], "manager-cursor"), new Page<AdvisorRequestSummaryView>([shared], null));
        var request = new ListAdvisorRequests(fixture.TenantId, fixture.ProgramId, Limit: 25);

        // Act
        var result = await new ListAdvisorRequestsHandler(directory,
                new AdvisorHandoffListReadConsistency(directory, fixture.Events),
                new FixedListAccess(AdvisorHandoffListVisibility.SharedOnly),
                new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(request,
                ProgramManagementServices.Actor(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([shared], result.Value.Items);
        Assert.Equal(0, directory.FullCalls);
        Assert.Equal(1, directory.SharedCalls);
    }

    [Fact]
    public async Task ShouldDenyAdvisorListGivenGrantRevokedDuringRead()
    {
        // Arrange
        await using var fixture = new Fixture();
        var shared = new AdvisorRequestSummaryView(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), "Shared review", "ready_for_review", Now);
        var directory = new FixedDirectory(new Page<AdvisorRequestSummaryView>([], null),
            new Page<AdvisorRequestSummaryView>([shared], null));
        var access = new SequenceListAccess(AdvisorHandoffListVisibility.SharedOnly,
            AdvisorHandoffListVisibility.Denied);
        var handler = new ListAdvisorRequestsHandler(directory,
            new AdvisorHandoffListReadConsistency(directory, fixture.Events), access,
            new FixedTimeProvider(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListAdvisorRequests>(
            new ListAdvisorRequests(fixture.TenantId, fixture.ProgramId),
            ProgramManagementServices.Actor(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(2, access.Calls);
        Assert.Equal(0, directory.FullCalls);
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
                new AdvisorHandoffListReadConsistency(directory, fixture.Events),
                new FixedListAccess(AdvisorHandoffListVisibility.Full), new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(new ListAdvisorRequests(
                fixture.TenantId, fixture.ProgramId),
                ProgramManagementServices.Actor(Uuid.CreateVersion4())), CancellationToken.None);

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
                new AdvisorHandoffListReadConsistency(directory, fixture.Events),
                new FixedListAccess(AdvisorHandoffListVisibility.Denied),
                new FixedTimeProvider(Now))
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
                new AdvisorHandoffListReadConsistency(directory, fixture.Events),
                new FixedListAccess(AdvisorHandoffListVisibility.Full), new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<ListAdvisorRequests>(new ListAdvisorRequests(
                fixture.TenantId, fixture.ProgramId),
                ProgramManagementServices.Actor(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(0, directory.Calls);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly AdvisorHost _host = new();

        public Uuid TenantId => _host.TenantId;
        public Uuid ProgramId => _host.ProgramId;
        public IDomainEventReader Events => _host.Events;

        public async Task SeedHandoffAsync() =>
            Assert.True((await _host.CreateAsync(Uuid.CreateVersion4(), [AdvisorHost.NewSource()], "Review")).IsSuccess);

        public ValueTask DisposeAsync() => _host.DisposeAsync();
    }

    sealed class FixedDirectory(Page<AdvisorRequestSummaryView> page,
        Page<AdvisorRequestSummaryView>? sharedPage = null)
        : IAdvisorHandoffDirectoryReader
    {
        public int Calls { get; private set; }
        public int FullCalls { get; private set; }
        public int SharedCalls { get; private set; }
        public int LastLimit { get; private set; }
        public string? LastCursor { get; private set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AdvisorRequestSummaryView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            Calls++;
            FullCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            return ValueTask.FromResult(page);
        }

        public ValueTask<Page<AdvisorRequestSummaryView>> ListSharedProgramAsync(Uuid tenantId,
            Uuid programId, DateOnly asOf, int limit, string? cursor,
            CancellationToken ct = default)
        {
            Calls++;
            SharedCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            return ValueTask.FromResult(sharedPage ?? new Page<AdvisorRequestSummaryView>([], null));
        }
    }

    sealed class FixedListAccess(AdvisorHandoffListVisibility visibility)
        : IAdvisorHandoffListAccess
    {
        public ValueTask<AdvisorHandoffListVisibility> GetListVisibilityAsync(Uuid tenantId,
            Uuid programId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(visibility);
    }

    sealed class SequenceListAccess(params AdvisorHandoffListVisibility[] results)
        : IAdvisorHandoffListAccess
    {
        public int Calls { get; private set; }

        public ValueTask<AdvisorHandoffListVisibility> GetListVisibilityAsync(Uuid tenantId,
            Uuid programId, Uuid userId, CancellationToken ct = default)
        {
            var index = Math.Min(Calls, results.Length - 1);
            Calls++;
            return ValueTask.FromResult(results[index]);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
