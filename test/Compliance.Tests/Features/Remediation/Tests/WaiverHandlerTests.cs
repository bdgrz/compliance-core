using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Remediation.Readers;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class WaiverHandlerTests
{
    [Fact]
    public async Task ShouldPersistReviewedWaiverAndComputeExpiryGivenCanonicalMemberCommands()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requester = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();
        var request = new RequestWaiver(Uuid.CreateVersion4(), tenant, "control_occurrence",
            [new WaiverScopeRecord("control_occurrence", Uuid.CreateVersion4(), 1)], "Temporary exception",
            "Daily manual follow-up", fixture.Clock.Now.AddDays(1));
        var requested = await new RequestWaiverHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Context(request, requester), CancellationToken.None);
        Assert.True(requested.IsSuccess, requested.Error?.Message);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        var review = new ReviewWaiver(Uuid.CreateVersion4(), tenant, request.WaiverId, 1, true);
        var reader = new WaiverReader(fixture.Reader, fixture.Clock);
        var get = new GetWaiverHandler(reader);

        // Act
        var approved = await new ReviewWaiverHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Context(review, approver), CancellationToken.None);
        var current = await get.HandleAsync(new Cntryl.Portia.RequestContext<GetWaiver>(new GetWaiver(tenant, request.WaiverId)),
            CancellationToken.None);
        var foreign = await get.HandleAsync(new Cntryl.Portia.RequestContext<GetWaiver>(new GetWaiver(Uuid.CreateVersion4(), request.WaiverId)),
            CancellationToken.None);
        fixture.Clock.Now = request.ExpiresAt;
        var expired = await reader.ReadAsync(tenant, request.WaiverId);

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.True(current.Value.IsEffective);
        Assert.Equal(RbacIds.Member(tenant, requester).ToString(), current.Value.RequestedBy.Id);
        Assert.Equal(RbacIds.Member(tenant, approver).ToString(), current.Value.ReviewedBy!.Id);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error?.Kind);
        Assert.Equal("expired", expired!.State);
        Assert.False(expired.IsEffective);
        Assert.Equal(2, expired.Revision);
    }

    [Theory]
    [InlineData("mcp")]
    [InlineData("direct")]
    [InlineData("system")]
    public async Task ShouldRefusePersonalDecisionsWithoutAppendGivenNonHttpOrSystemExecution(string transport)
    {
        // Arrange
        await using var fixture = new Fixture();
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var actor = transport == "system" ? RequestActor.System : Actor(Uuid.CreateVersion4());
        RequestInvocation invocation = transport switch
        {
            "mcp" => new McpInvocation("review-waiver"),
            "direct" => new DirectInvocation(),
            _ => new HttpInvocation("POST", "/waivers/review", "/waivers/review", "review"),
        };
        var review = new ReviewWaiver(Uuid.CreateVersion4(), tenant, id, 1, true);
        var revoke = new RevokeWaiver(Uuid.CreateVersion4(), tenant, id, 2, "Reason");

        // Act
        var reviewed = await new ReviewWaiverHandler(fixture.Executor, fixture.Clock).HandleAsync(
            new Cntryl.Portia.RequestContext<ReviewWaiver>(review, new RequestDispatchContext(actor, invocation)), CancellationToken.None);
        var revoked = await new RevokeWaiverHandler(fixture.Executor, fixture.Clock).HandleAsync(
            new Cntryl.Portia.RequestContext<RevokeWaiver>(revoke, new RequestDispatchContext(actor, invocation)), CancellationToken.None);
        var owner = await fixture.Reader.HydrateAsync(new Waiver(tenant, id));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, reviewed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, revoked.Error?.Kind);
        Assert.Equal(0UL, owner.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRevokeAndKeepAcceptedRetryGivenApprovedWaiver()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requester = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var request = new RequestWaiver(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "data_flow_encryption",
            [new WaiverScopeRecord("data_flow", Uuid.CreateVersion4(), 1)], "Temporary exception",
            "Isolated network", fixture.Clock.Now.AddDays(1));
        Assert.True((await new RequestWaiverHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Context(request, requester), CancellationToken.None)).IsSuccess);
        var review = new ReviewWaiver(Uuid.CreateVersion4(), request.TenantId, request.WaiverId, 1, true);
        Assert.True((await new ReviewWaiverHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Context(review, reviewer), CancellationToken.None)).IsSuccess);
        var command = new RevokeWaiver(Uuid.CreateVersion4(), request.TenantId, request.WaiverId, 2, "Compensation ended");
        var handler = new RevokeWaiverHandler(fixture.Executor, fixture.Clock);

        // Act
        var revoked = await handler.HandleAsync(Context(command, reviewer), CancellationToken.None);
        fixture.Clock.Now = request.ExpiresAt.AddDays(1);
        var replay = await handler.HandleAsync(Context(command, reviewer), CancellationToken.None);
        var view = await new WaiverReader(fixture.Reader, fixture.Clock).ReadAsync(request.TenantId, request.WaiverId);

        // Assert
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal("revoked", view!.State);
        Assert.False(view.IsEffective);
        Assert.Equal(3, view.Revision);
        Assert.Equal("Compensation ended", view.RevocationReason);
    }

    static ClaimsPrincipal Actor(Uuid user) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()), new Claim("name", "Member")], "BdgrzSession"));

    static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid user) where T : IRequestBase, IIdentifiedRequest =>
        new(request, new RequestDispatchContext(Actor(user),
            new HttpInvocation("POST", "/waivers", "/waivers", "waiver"),
            new RequestMetadata(request.RequestId, Uuid.CreateVersion4(), null)));

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public FixtureClock Clock { get; } = new();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixtureClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
