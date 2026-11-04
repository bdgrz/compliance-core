using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Handlers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class WithdrawEvidenceSupportHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldWithdrawWithoutChangingHistoricalAssertionGivenAuthorizedLead()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedMemberAsync();
        var link = await fixture.SeedLinkAsync();
        var reads = new EvidenceSupportReader(fixture.Reader);
        var before = await reads.ReadOriginalAsync(fixture.TenantId, fixture.ProgramId, link.CommandId);
        var request = fixture.Request(link.CommandId);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var active = await reads.RequireActiveAsync(fixture.TenantId, fixture.ProgramId, link.CommandId);
        var historical = await reads.ReadOriginalAsync(fixture.TenantId, fixture.ProgramId, link.CommandId);
        var restored = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, active.Error?.Kind);
        Assert.Equal(before.Value, historical.Value);
        Assert.Equal(7, historical.Value.SupportedRecordRevision);
        Assert.Equal(2, historical.Value.ArtifactRevision);
        Assert.Equal(Now.AddHours(-1), historical.Value.LinkedAt);
        Assert.Equal(2, restored.CommittedStreamPosition);
        Assert.Equal("Wrong target version", restored.Withdrawal!.Reason);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), restored.Withdrawal.ActorMemberId);
        Assert.Equal(EvidenceSupportPermissions.Withdraw, Assert.Single(fixture.Permissions.Requested));
    }

    [Fact]
    public async Task ShouldReturnOriginalActiveSupportGivenNoWithdrawal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var link = await fixture.SeedLinkAsync();
        var reads = new EvidenceSupportReader(fixture.Reader);

        // Act
        var active = await reads.RequireActiveAsync(fixture.TenantId, fixture.ProgramId, link.CommandId);
        var historical = await reads.ReadOriginalAsync(fixture.TenantId, fixture.ProgramId, link.CommandId);

        // Assert
        Assert.True(active.IsSuccess, active.Error?.Message);
        Assert.Equal(historical.Value, active.Value);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("firm_staff")]
    [InlineData("missing_member")]
    [InlineData("system")]
    [InlineData("anonymous")]
    public async Task ShouldRefuseWithoutFactGivenActorLacksCurrentLeadAuthority(string denied)
    {
        // Arrange
        await using var fixture = new Fixture();
        if (denied != "missing_member")
            await fixture.SeedMemberAsync(denied == "firm_staff" ? "firm_staff" : "client_personnel");
        fixture.Permissions.Allowed = denied != "permission";
        var link = await fixture.SeedLinkAsync();
        var request = fixture.Request(link.CommandId);
        var context = denied switch
        {
            "system" => fixture.Context(request, RequestActor.System),
            "anonymous" => fixture.Context(request, new ClaimsPrincipal()),
            _ => fixture.Context(request),
        };

        // Act
        var result = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var owner = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(1UL, owner.CommittedStreamPosition);
        Assert.Null(owner.Withdrawal);
        Assert.True(owner.IsActive);
    }

    [Fact]
    public async Task ShouldCheckCurrentPermissionBeforeReplayGivenPreviouslyAcceptedWithdrawal()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedMemberAsync();
        var link = await fixture.SeedLinkAsync();
        var request = fixture.Request(link.CommandId);
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        fixture.Permissions.Allowed = false;

        // Act
        var replay = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, replay.Error?.Kind);
        Assert.Equal(2UL, owner.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldKeepExactRetryAndRejectChangedReasonGivenCommittedWithdrawal()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedMemberAsync();
        var link = await fixture.SeedLinkAsync();
        var request = fixture.Request(link.CommandId);
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);

        // Act
        var replay = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var changed = await fixture.Handler().HandleAsync(fixture.Context(request with { Reason = "Other" }), CancellationToken.None);
        var owner = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(2UL, owner.CommittedStreamPosition);
        Assert.Equal("Wrong target version", owner.Withdrawal!.Reason);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("support")]
    public async Task ShouldNotRevealOriginalOrWithdrawGivenForeignScope(string foreign)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedMemberAsync();
        var link = await fixture.SeedLinkAsync();
        var request = fixture.Request(link.CommandId);
        request = foreign switch
        {
            "tenant" => request with { TenantId = Uuid.CreateVersion4() },
            "program" => request with { ProgramId = Uuid.CreateVersion4() },
            _ => request with { SupportId = Uuid.CreateVersion4() },
        };
        var reads = new EvidenceSupportReader(fixture.Reader);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var historical = await reads.ReadOriginalAsync(request.TenantId, request.ProgramId, request.SupportId);
        var owner = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.Equal(foreign == "tenant" ? RequestErrorKind.Forbidden : RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, historical.Error?.Kind);
        Assert.Equal(1UL, owner.CommittedStreamPosition);
        Assert.Null(owner.Withdrawal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutAppendGivenInvalidReasonOrStaleRevision(bool stale)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedMemberAsync();
        var link = await fixture.SeedLinkAsync();
        var request = fixture.Request(link.CommandId);
        request = stale ? request with { ExpectedRevision = 0 } : request with { Reason = " " };

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.Equal(stale ? RequestErrorKind.Conflict : RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1UL, owner.CommittedStreamPosition);
        Assert.Null(owner.Withdrawal);
    }

    [Fact]
    public async Task ShouldCommitOnlyOneWithdrawalGivenCompetingAppends()
    {
        // Arrange
        await using var fixture = new Fixture();
        var link = await fixture.SeedLinkAsync();
        var first = await fixture.ReadAsync(link.CommandId);
        var competing = await fixture.ReadAsync(link.CommandId);
        var actor = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Lead");
        Assert.Null(first.Withdraw(fixture.Request(link.CommandId), actor, Now));
        Assert.Null(competing.Withdraw(fixture.Request(link.CommandId) with { Reason = "Other" }, actor, Now));
        await fixture.Writer.SaveAsync(first, new RequestDispatchContext(fixture.Actor));

        // Act
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () =>
            await fixture.Writer.SaveAsync(competing, new RequestDispatchContext(fixture.Actor)));
        var committed = await fixture.ReadAsync(link.CommandId);

        // Assert
        Assert.Equal(2UL, committed.CommittedStreamPosition);
        Assert.Equal("Wrong target version", committed.Withdrawal!.Reason);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Permissions Permissions { get; } = new();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()), new Claim("name", "Evidence lead")], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public WithdrawEvidenceSupportHandler Handler() => new(
            _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), Reader, Permissions, TimeProvider.System);

        public WithdrawEvidenceSupport Request(Uuid supportId) =>
            new(Uuid.CreateVersion4(), TenantId, ProgramId, supportId, 1, "Wrong target version");

        public Cntryl.Portia.RequestContext<WithdrawEvidenceSupport> Context(WithdrawEvidenceSupport request,
            ClaimsPrincipal? actor = null) => new(request, new RequestDispatchContext(actor ?? Actor,
                new HttpInvocation("POST", "/evidence/support/withdraw", "/evidence/support/withdraw", "withdraw-support")));

        public ValueTask<EvidenceSupport> ReadAsync(Uuid id) =>
            Reader.HydrateAsync(new EvidenceSupport(TenantId, ProgramId, id));

        public async Task SeedMemberAsync(string affiliation = "client_personnel")
        {
            var member = new Member(TenantId, UserId);
            Assert.True(member.Register(NativeFacts.Root(member, Uuid.CreateVersion4(), "RegisterMember", 0),
                NativeFacts.Ref(), affiliation).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<LinkEvidenceSupport> SeedLinkAsync()
        {
            var link = new LinkEvidenceSupport(Uuid.CreateVersion4(), TenantId, ProgramId, Uuid.CreateVersion4(),
                "control", Uuid.CreateVersion4(), 7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                "Design evidence", "Exact target rationale", 0);
            var support = new EvidenceSupport(TenantId, ProgramId, link.CommandId);
            Assert.Null(support.Link(link, 2, new string('a', 64),
                ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Evidence lead"), Now.AddHours(-1)));
            await Writer.SaveAsync(support, new RequestDispatchContext(RequestActor.System));
            return link;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public List<string> Requested { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default)
        {
            Requested.Add(permission);
            return ValueTask.FromResult(Allowed);
        }
    }
}
