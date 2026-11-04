using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipLifecycleHandlerTests
{
    static readonly DateOnly Start = new(2026, 1, 5);

    [Theory]
    [InlineData("start", "pending", typeof(WorkRelationshipStarted))]
    [InlineData("leave", "active", typeof(WorkRelationshipLeaveStarted))]
    [InlineData("return", "on_leave", typeof(WorkRelationshipLeaveEnded))]
    [InlineData("end", "active", typeof(WorkRelationshipEnded))]
    public async Task ShouldInvokeOwnerWithActorGivenLifecycleCommand(string command, string status,
        Type eventType)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var relationship = New(tenant, status);
        var executor = new OwnerExecutor(relationship);
        var before = new AggregateScenario<WorkRelationship>(relationship).PendingEvents.Count;

        // Act
        var result = await SendAsync(command, executor, tenant, relationship.Id, user, 1);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, executor.Calls);
        Assert.Equal(AggregateDisposition.Commit, executor.Disposition);
        var facts = new AggregateScenario<WorkRelationship>(relationship).PendingEvents;
        Assert.Equal(before + 1, facts.Count);
        Assert.Equal(eventType, facts[^1].GetType());
        var actor = facts[^1] switch
        {
            WorkRelationshipStarted started => started.Actor,
            WorkRelationshipLeaveStarted leave => leave.Actor,
            WorkRelationshipLeaveEnded returned => returned.Actor,
            WorkRelationshipEnded ended => ended.Actor,
            _ => throw new InvalidOperationException("Expected a named lifecycle fact."),
        };
        Assert.Equal(RbacIds.Member(tenant, user).ToString(), actor.Id);
    }

    [Theory]
    [InlineData("start", "pending")]
    [InlineData("leave", "active")]
    [InlineData("return", "on_leave")]
    [InlineData("end", "active")]
    public async Task ShouldReturnConflictWithoutFactGivenStaleLifecycleCommand(string command,
        string status)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var relationship = New(tenant, status);
        var executor = new OwnerExecutor(relationship);
        var before = new AggregateScenario<WorkRelationship>(relationship).PendingEvents.ToArray();

        // Act
        var result = await SendAsync(command, executor, tenant, relationship.Id, Uuid.CreateVersion4(), 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(AggregateDisposition.Discard, executor.Disposition);
        Assert.Equal(before, new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    static WorkRelationship New(Uuid tenant, string status)
    {
        var relationship = new WorkRelationship(tenant, WorkRelationship.IdFor(tenant, "E-100"));
        Assert.True(relationship.Record(Uuid.CreateVersion4(), "E-100",
            new WorkRelationshipTerms("employee", status, Start, null, null, null, null),
            Bdgrz.Compliance.Features.AccessControl.Models.ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"),
            DateTimeOffset.UtcNow).IsSuccess);
        return relationship;
    }

    static ValueTask<Result> SendAsync(string command, OwnerExecutor executor, Uuid tenant,
        Uuid relationship, Uuid user, long revision)
    {
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));
        return command switch
        {
            "start" => new StartWorkRelationshipHandler(executor, TimeProvider.System).HandleAsync(
                new RequestContext<StartWorkRelationship>(new(tenant, relationship, revision, Start), actor), CancellationToken.None),
            "leave" => new StartLeaveHandler(executor, TimeProvider.System).HandleAsync(
                new RequestContext<StartLeave>(new(tenant, relationship, revision, Start), actor), CancellationToken.None),
            "return" => new EndLeaveHandler(executor, TimeProvider.System).HandleAsync(
                new RequestContext<EndLeave>(new(tenant, relationship, revision, Start), actor), CancellationToken.None),
            "end" => new EndWorkRelationshipHandler(executor, TimeProvider.System).HandleAsync(
                new RequestContext<EndWorkRelationship>(new(tenant, relationship, revision, Start, "Ended"), actor), CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    sealed class OwnerExecutor(WorkRelationship owner) : IAggregateExecutor
    {
        public int Calls { get; private set; }
        public AggregateDisposition? Disposition { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Assert.IsType<WorkRelationship>(aggregate);
            Assert.Equal(owner.Stream, aggregate.Stream);
            Calls++;
            var outcome = operation((TAggregate)(Aggregate)owner);
            Disposition = outcome.Disposition;
            return ValueTask.FromResult(outcome.Result);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            throw new InvalidOperationException("The lifecycle command does not return an entity value.");
    }
}
