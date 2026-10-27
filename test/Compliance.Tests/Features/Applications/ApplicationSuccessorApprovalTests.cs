using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationSuccessorApprovalTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldDenyApprovalGivenMcpInvocation()
    {
        // Arrange
        var scenario = Scenario.Create();
        var executor = new NoWriteExecutor();
        var handler = scenario.Handler(executor, IncompleteRelationshipImpactReader());
        var context = scenario.Context(new McpInvocation("synthetic.application.successor.approve"));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(executor.Called);
        Assert.Equal(scenario.SourceEventCount, scenario.PendingSourceEvents());
    }

    [Fact]
    public async Task ShouldBlockApprovalWithoutAppendGivenPendingApplicationRelationshipImpact()
    {
        // Arrange
        var scenario = Scenario.Create();
        var executor = new NoWriteExecutor();
        var handler = scenario.Handler(executor, IncompleteRelationshipImpactReader());
        var context = scenario.Context(new HttpInvocation("POST", "/synthetic/successor/approve",
            "/synthetic/successor/approve", "synthetic"));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.False(executor.Called);
        Assert.Equal(scenario.SourceEventCount, scenario.PendingSourceEvents());
    }

    [Fact]
    public async Task ShouldBlockApprovalWithoutAppendGivenRelationshipsChangeDuringImpactRead()
    {
        // Arrange
        var scenario = Scenario.Create();
        var executor = new NoWriteExecutor();
        var impact = scenario.CompleteImpact();
        var handler = scenario.Handler(executor,
            new ImpactReader(impact, scenario.AddUnrelatedRelationship));
        var context = scenario.Context(new HttpInvocation("POST", "/synthetic/successor/approve",
            "/synthetic/successor/approve", "synthetic"));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(executor.Called);
        Assert.Equal(scenario.SourceEventCount + 1, scenario.PendingSourceEvents());
    }

    [Fact]
    public async Task ShouldPersistAttributedApprovalGivenCurrentCompleteImpact()
    {
        // Arrange
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider;
        var tenantId = Uuid.CreateVersion4();
        var predecessorId = Uuid.CreateVersion4();
        var successorId = Uuid.CreateVersion4();
        var proposerUserId = Uuid.CreateVersion4();
        var approverUserId = Uuid.CreateVersion4();
        var proposerMemberId = RbacIds.Member(tenantId, proposerUserId);
        var approverMemberId = RbacIds.Member(tenantId, approverUserId);
        var now = DateTimeOffset.UtcNow;
        var predecessor = new DeclaredApplication(tenantId, predecessorId);
        var successor = new DeclaredApplication(tenantId, successorId);
        Assert.True(predecessor.Declare("Payroll", "Payroll processing", null,
            proposerMemberId, "Proposer", now).IsSuccess);
        Assert.True(successor.Declare("Payroll successor", "Payroll processing", null,
            approverMemberId, "Approver", now).IsSuccess);
        var proposal = predecessor.RecordRelationship(successorId, 1, 1, "replaces",
            proposerMemberId, "Proposer", now.AddMinutes(1));
        Assert.True(proposal.IsSuccess);
        var writer = service.GetRequiredService<IAggregateWriter>();
        await writer.SaveAsync(predecessor, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        await writer.SaveAsync(successor, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var digest = new string('b', 64);
        var impact = new ApplicationChangePreview(tenantId, predecessorId, 1, "retire",
            [], [], [], true)
        {
            ImpactDigest = digest,
        };
        var reader = service.GetRequiredService<IAggregateReader>();
        var handler = new ApproveApplicationSuccessorHandler(
            service.GetRequiredService<IAggregateExecutor>(), reader, new ImpactReader(impact),
            TimeProvider.System);
        var request = new ApproveApplicationSuccessor(tenantId, predecessorId, successorId,
            1, 1, proposal.Value.Revision, digest);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "bdgrz"),
            new Claim("sub", approverUserId.ToString()),
            new Claim("email", "approver@example.com"),
        ], "BdgrzSession"));
        var context = new InvocationContext<ApproveApplicationSuccessor>(request, actor,
            new HttpInvocation("POST", "/synthetic/successor/approve",
                "/synthetic/successor/approve", "synthetic"));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var persisted = await reader.HydrateAsync(
            new DeclaredApplication(tenantId, predecessorId));
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(new EventStreamAddress(
                           tenantId.ToString(), "applications", predecessorId.ToString()), 0,
                           CancellationToken.None))
            records.Add(record);

        // Assert
        Assert.True(result.IsSuccess);
        var approved = Assert.IsType<ApplicationRelationshipApproved>(
            Assert.Single(records, record => record.Event is ApplicationRelationshipApproved).Event);
        Assert.Equal(tenantId, approved.TenantId);
        Assert.Equal(proposal.Value.RelationshipId, approved.RelationshipId);
        Assert.Equal(predecessorId, approved.SourceApplicationId);
        Assert.Equal(successorId, approved.TargetApplicationId);
        Assert.Equal(2, approved.Revision);
        Assert.Equal(1, approved.SourceApplicationRevision);
        Assert.Equal(1, approved.TargetApplicationRevision);
        Assert.Equal(digest, approved.ImpactDigest);
        Assert.Equal(approverMemberId, approved.ApproverMemberId);
        Assert.Equal("approver@example.com", approved.ApproverDisplay);
        Assert.Equal(ActorReference.ForMember(approverMemberId, "approver@example.com"),
            approved.Approver);
        var relationship = Assert.IsType<ApplicationRelationshipView>(
            persisted.Relationship("replaces", successorId));
        Assert.Equal("approved", relationship.Status);
        Assert.Equal(approved.Approver, Assert.IsType<ApplicationRelationshipApproval>(
            relationship.Approval).Approver);
        Assert.Equal(digest, relationship.Approval.ImpactDigest);
    }

    static ImpactReader IncompleteRelationshipImpactReader() => new(new ApplicationChangePreview(
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "retire", [], [],
        ["application_relationships"], false)
    {
        ImpactDigest = new string('a', 64),
    });

    sealed class Scenario
    {
        readonly ClaimsPrincipal _actor;

        Scenario(DeclaredApplication source, DeclaredApplication successor,
            Uuid tenantId, Uuid predecessorId, Uuid successorId, Uuid memberId,
            ClaimsPrincipal actor)
        {
            Source = source;
            Successor = successor;
            TenantId = tenantId;
            PredecessorId = predecessorId;
            SuccessorId = successorId;
            MemberId = memberId;
            _actor = actor;
            SourceEventCount = PendingSourceEvents();
        }

        DeclaredApplication Source { get; }
        DeclaredApplication Successor { get; }
        Uuid TenantId { get; }
        Uuid PredecessorId { get; }
        Uuid SuccessorId { get; }
        Uuid MemberId { get; }
        public int SourceEventCount { get; }

        public static Scenario Create()
        {
            var tenantId = Uuid.CreateVersion4();
            var predecessorId = Uuid.CreateVersion4();
            var successorId = Uuid.CreateVersion4();
            var userId = Uuid.CreateVersion4();
            var proposerUserId = Uuid.CreateVersion4();
            var memberId = RbacIds.Member(tenantId, proposerUserId);
            var source = new DeclaredApplication(tenantId, predecessorId);
            var successor = new DeclaredApplication(tenantId, successorId);
            Assert.True(source.Declare("Payroll", "Payroll processing", null, memberId,
                "Preparer", Now).IsSuccess);
            Assert.True(successor.Declare("Payroll successor", "Payroll processing", null,
                RbacIds.Member(tenantId, userId), "Partner", Now).IsSuccess);
            Assert.True(source.RecordRelationship(successorId, 1, 1, "replaces", memberId,
                "Preparer", Now).IsSuccess);
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())],
                "BdgrzSession"));
            return new Scenario(source, successor, tenantId, predecessorId, successorId,
                memberId, actor);
        }

        public int PendingSourceEvents() =>
            new AggregateScenario<DeclaredApplication>(Source).PendingEvents.Count;

        public ApplicationChangePreview CompleteImpact() =>
            new(TenantId, PredecessorId, 1, "retire", [], [], [], true)
            {
                ImpactDigest = new string('a', 64),
            };

        public void AddUnrelatedRelationship()
        {
            var result = Source.RecordRelationship(Uuid.CreateVersion4(), 1, 1,
                "depends_on", Uuid.CreateVersion4(), "Manager", Now.AddMinutes(1));
            Assert.True(result.IsSuccess);
        }

        public ApproveApplicationSuccessorHandler Handler(IAggregateExecutor executor,
            IApplicationChangeImpactReader impactReader) => new(executor,
            new ApplicationReader(Source, Successor), impactReader, TimeProvider.System);

        public InvocationContext<ApproveApplicationSuccessor> Context(RequestInvocation invocation) =>
            new(new ApproveApplicationSuccessor(
                TenantId, PredecessorId, SuccessorId, 1, 1, 1, new string('a', 64)),
                _actor, invocation);
    }

    sealed class ApplicationReader(params DeclaredApplication[] applications) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var current = applications.FirstOrDefault(application =>
                application.Stream == aggregate.Stream);
            return ValueTask.FromResult(current is null
                ? aggregate
                : (TAggregate)(Aggregate)current);
        }
    }

    sealed class ImpactReader(ApplicationChangePreview preview, Action? onRead = null)
        : IApplicationChangeImpactReader
    {
        public ValueTask<Result<ApplicationChangePreview>> ReadAsync(
            PreviewApplicationChange request, Uuid userId, CancellationToken ct)
        {
            onRead?.Invoke();
            return ValueTask.FromResult(Result<ApplicationChangePreview>.Success(preview with
            {
                TenantId = request.TenantId,
                ApplicationId = request.ApplicationId,
                ApplicationRevision = request.ExpectedApplicationRevision,
            }));
        }
    }

    sealed class NoWriteExecutor : IAggregateExecutor
    {
        public bool Called { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("An incomplete approval cannot append events.");
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("An incomplete approval cannot append events.");
        }
    }

    sealed class InvocationContext<TRequest>(TRequest request, ClaimsPrincipal actor,
        RequestInvocation invocation) : IRequestContext<TRequest>
    {
        public TRequest Request { get; } = request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation => invocation;
    }
}
