using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipNamedChangeHandlerTests
{
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("active", "employee", true)]
    [InlineData("active", "external_collaborator", false)]
    [InlineData("pending", "employee", false)]
    [InlineData("on_leave", "employee", false)]
    [InlineData("ended", "employee", false)]
    [InlineData(null, "employee", false)]
    public async Task ShouldRequireActiveSponsorGivenNamedSponsorChange(string? status, string workerType, bool accepted)
    {
        // Arrange
        var fixture = new Fixture();
        if (status is not null)
            await fixture.ProjectSponsorAsync(status, workerType);
        var handler = new ChangeSponsorHandler(fixture.Executor, fixture.Reader, fixture.Roster,
            fixture.Consistency, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ChangeSponsor(fixture.Tenant,
            fixture.Owner.Id, 1, fixture.OtherPerson)), CancellationToken.None);

        // Assert
        Assert.Equal(accepted, result.IsSuccess);
        Assert.Equal(accepted ? 1 : 0, fixture.Executor.Calls);
        if (accepted)
        {
            var fact = Assert.IsType<WorkRelationshipSponsorChanged>(fixture.Facts[^1]);
            Assert.Equal(fixture.OtherPerson, fact.SponsorPersonId);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), fact.Actor.Id);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
            Assert.Single(fixture.Facts);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldCheckManagerPersonGivenNamedManagerChange(bool recorded)
    {
        // Arrange
        var fixture = new Fixture();
        var manager = recorded ? fixture.OtherPerson : Uuid.CreateVersion4();
        var handler = new ChangeManagerHandler(fixture.Executor, fixture.Reader, new FieldPermissions(), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ChangeManager(fixture.Tenant,
            fixture.Owner.Id, 1, manager)), CancellationToken.None);

        // Assert
        Assert.Equal(recorded, result.IsSuccess);
        Assert.Equal(recorded ? 1 : 0, fixture.Executor.Calls);
        if (recorded)
            Assert.Equal(manager, Assert.IsType<WorkRelationshipManagerChanged>(fixture.Facts[^1]).ManagerPersonId);
        else
        {
            Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
            Assert.Single(fixture.Facts);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutFactGivenManagerComparisonWithoutRestrictedGrant(bool matching)
    {
        // Arrange
        var fixture = new Fixture();
        var handler = new ChangeManagerHandler(fixture.Executor, fixture.Reader,
            new FieldPermissions(false), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ChangeManager(fixture.Tenant,
            fixture.Owner.Id, 1, matching ? null : fixture.OtherPerson)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Single(fixture.Facts);
        Assert.Equal(1, fixture.Owner.Revision);
    }

    [Theory]
    [InlineData("department")]
    [InlineData("dates")]
    public async Task ShouldInvokeOwnerWithActorGivenNamedChange(string kind)
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = kind == "department"
            ? await new ChangeDepartmentHandler(fixture.Executor, TimeProvider.System).HandleAsync(
                fixture.Context(new ChangeDepartment(fixture.Tenant, fixture.Owner.Id, 1, "Finance")), CancellationToken.None)
            : await new CorrectWorkRelationshipDatesHandler(fixture.Executor, TimeProvider.System).HandleAsync(
                fixture.Context(new CorrectWorkRelationshipDates(fixture.Tenant, fixture.Owner.Id, 1,
                    Start.AddDays(1), null, "Correction")), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, fixture.Executor.Calls);
        var actor = fixture.Facts[^1] switch
        {
            WorkRelationshipDepartmentChanged department => department.Actor,
            WorkRelationshipDatesCorrected dates => dates.Actor,
            _ => throw new InvalidOperationException("Expected a named change."),
        };
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), actor.Id);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenSponsorPersonNotRecorded()
    {
        // Arrange
        var fixture = new Fixture();
        var handler = new ChangeSponsorHandler(fixture.Executor, fixture.Reader, fixture.Roster,
            fixture.Consistency, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ChangeSponsor(fixture.Tenant,
            fixture.Owner.Id, 1, Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Single(fixture.Facts);
    }

    sealed class Fixture
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Person { get; } = Uuid.CreateVersion4();
        public Uuid OtherPerson { get; } = Uuid.CreateVersion4();
        public WorkRelationship Owner { get; }
        public OwnerExecutor Executor { get; }
        public RecordedPeopleReader Reader { get; }
        public FitzWorkforceObservationDirectory Roster { get; } = new(new InMemoryKvClient());
        public WorkforceObservationReadConsistency Consistency => new(Roster, new InMemoryEventStore());
        public IReadOnlyList<DomainEvent> Facts => new AggregateScenario<WorkRelationship>(Owner).PendingEvents;

        public Fixture()
        {
            Owner = new WorkRelationship(Tenant, WorkRelationship.IdFor(Tenant, "E-100"));
            Assert.True(Owner.Record(Person, "E-100", new WorkRelationshipTerms("external_collaborator",
                "active", Start, null, "Engineering", null, Uuid.CreateVersion4()), Author, DateTimeOffset.UtcNow).IsSuccess);
            Executor = new OwnerExecutor(Owner);
            Reader = new RecordedPeopleReader(Owner, [Person, OtherPerson]);
        }

        public RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", User.ToString())], "BdgrzSession")));

        public async Task ProjectSponsorAsync(string status, string workerType)
        {
            await using var batch = await Roster.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("WorkforceObservationsV1",
                    EventStreamPattern.ForPattern(Tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
            await Roster.ApplyAsync(new WorkRelationshipRecorded(Tenant, WorkRelationship.IdFor(Tenant, "SPONSOR"),
                OtherPerson, "SPONSOR", new WorkRelationshipTerms(workerType, status, Start,
                    status == "ended" ? Start : null, null, null,
                    workerType == "external_collaborator" ? Uuid.CreateVersion4() : null), Author, DateTimeOffset.UtcNow));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
    }

    sealed class RecordedPeopleReader(WorkRelationship owner, HashSet<Uuid> people) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is WorkRelationship)
                return ValueTask.FromResult((TAggregate)(Aggregate)owner);
            if (aggregate is Person person && people.Contains(person.Id))
            {
                var fact = DomainEventSeed.Attach(new PersonRecorded(person.TenantId, person.Id,
                    "Recorded person", null, Author, DateTimeOffset.UtcNow), person.Id, 1);
                return ValueTask.FromResult((TAggregate)(Aggregate)new AggregateScenario<Person>(person).Given(fact).Aggregate);
            }
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class FieldPermissions(bool allowed = true) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed && permission == FieldClasses.WorkforceManagerChain.ReadPermission);
    }

    sealed class OwnerExecutor(WorkRelationship owner) : IAggregateExecutor
    {
        public int Calls { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Assert.Equal(owner.Stream, aggregate.Stream);
            Calls++;
            return ValueTask.FromResult(operation((TAggregate)(Aggregate)owner).Result);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            throw new InvalidOperationException("A named change does not return an entity value.");
    }
}
