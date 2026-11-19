using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class PlatformOperatorOperationsTests
{
    [Fact]
    public async Task ShouldReplayGrantGivenDirectoryNoLongerFindsTheUser()
    {
        // Arrange
        var actorId = Uuid.CreateVersion4();
        var subject = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var original = new PlatformOperator(subject, operation);
        var scenario = new AggregateScenario<PlatformOperator>(original);
        Assert.True(original.Grant(operation, 0, actorId, new ActorAttribution(actorId.ToString(), "bdgrz"), "on call").IsSuccess);
        var reader = new Reader(scenario.PendingEvents.ToArray());
        var directory = new Directory();
        var service = new PlatformOperatorOperations(new Executor(reader), reader, directory, new PlatformOperatorAuthority([], true));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", actorId.ToString(), ClaimValueTypes.String, "bdgrz")], "test"));
        var exact = new GrantPlatformOperator(subject, "on call", operation, 0);
        var changed = exact with { Reason = "different" };

        // Act
        var retry = await service.GrantAsync(new RequestContext<GrantPlatformOperator>(exact, actor), CancellationToken.None);
        var conflict = await service.GrantAsync(new RequestContext<GrantPlatformOperator>(changed, actor), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(0, directory.Calls);
    }

    sealed class Directory : IPlatformUserDirectoryReader
    {
        public int Calls { get; private set; }
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(false);
        }
    }

    sealed class Reader(DomainEvent[] history) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            new AggregateScenario<T>(aggregate).Given(history);
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class Executor(Reader reader) : IAggregateExecutor
    {
        public async ValueTask<Result> ExecuteAsync<T>(T aggregate, Func<T, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where T : Aggregate =>
            operation(await reader.HydrateAsync(aggregate, ct)).Result;
        public ValueTask<Result<TOut>> ExecuteAsync<T, TOut>(T aggregate, Func<T, AggregateOutcome<TOut>> operation,
            IExecutionContext context, CancellationToken ct = default) where T : Aggregate => throw new NotSupportedException();
    }
}
