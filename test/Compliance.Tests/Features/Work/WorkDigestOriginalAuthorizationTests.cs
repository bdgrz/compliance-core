using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestOriginalAuthorizationTests
{
    [Fact]
    public async Task ShouldReturnOriginalRetryAuthorizationGivenExactCommandRetriedAfterAnotherUnknownAttempt()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var by = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var week = new DateOnly(2026, 10, 12);
        var at = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        await using var provider = ProgramManagementServices.Build(new Permissions(), portia => portia
            .AddRequestHandler<AuthorizeWorkDigestUnknownRetryHandler>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>(), services =>
            {
                services.AddSingleton<IPlatformOperatorAccess>(new Operators(by));
                services.AddSingleton<TimeProvider>(new Clock(at.AddMinutes(3)));
                services.AddSingleton(new WorkDigestDeliverySettings("https://app.example",
                    "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
                    4, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1)));
            });
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        async Task MutateAsync(Func<WorkDigestDispatch, bool> mutation)
        {
            var result = await executor.ExecuteAsync(new WorkDigestDispatch(tenant, member), dispatch =>
            {
                Assert.True(mutation(dispatch));
                return AggregateOutcome.Commit(Result.Success);
            }, new Cntryl.Portia.RequestContext<Mutation>(new Mutation(), RequestActor.System));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        await MutateAsync(dispatch => dispatch.Schedule(new WorkDigestScheduleWindow(week, "UTC", at),
            Uuid.CreateVersion4(), TimeSpan.FromDays(1), at));
        await MutateAsync(dispatch => dispatch.StartAttempt(week, at.AddMinutes(1)));
        await MutateAsync(dispatch => dispatch.RecordUnknown(week, at.AddMinutes(2), "ambiguous"));
        // Extra selected identity fields are deliberately raw JSON so this test executes against the old DTO too.
        var json = $$"""{"command_id":"{{command}}","tenant_id":"{{tenant}}","member_id":"{{member}}","week_of":"2026-10-12","evidence_reference":"receipt:original","rationale":"Verified external receipt","expected_revision":3}""";
        var request = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.AuthorizeWorkDigestUnknownRetry)!;
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", by.ToString())], "BdgrzSession"));
        var context = new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request,
            new RequestDispatchContext(actor, new HttpInvocation("PUT", "/synthetic/digest/retry", "/synthetic/digest/retry", "synthetic")));
        var handler = scope.ServiceProvider.GetRequiredService<AuthorizeWorkDigestUnknownRetryHandler>();
        var first = await handler.HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        await MutateAsync(dispatch => dispatch.StartAttempt(week, at.AddMinutes(4)));
        await MutateAsync(dispatch => dispatch.RecordUnknown(week, at.AddMinutes(5), "ambiguous_again"));

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new WorkDigestDispatch(tenant, member));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(1, replay.Value.Attempts);
        Assert.Equal(WorkDigestDispatch.RetryPending, replay.Value.Status);
        Assert.Equal(2, current.Read(week).Attempts);
        Assert.Equal(WorkDigestDispatch.Unknown, current.Read(week).Status);
    }
    sealed record Mutation : IRequest;
    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    sealed class Permissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class Operators(Uuid allowed) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(userId == allowed);
    }
}
