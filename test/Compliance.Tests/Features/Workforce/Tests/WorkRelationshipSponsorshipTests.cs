using Bdgrz.Compliance.Features.Workforce.Projections;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipSponsorshipTests
{
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("active", "employee", false)]
    [InlineData("ended", "employee", true)]
    [InlineData("on_leave", "employee", true)]
    [InlineData("active", "external_collaborator", true)]
    [InlineData(null, "employee", true)]
    public async Task ShouldShowUnsponsoredWorkGivenSponsorEligibility(string? status, string workerType,
        bool expected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        if (status is not null)
            await fixture.SeedSponsorAsync(status, workerType);

        // Act
        var result = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var view = Assert.Single(result.Value);
        Assert.Equal(expected, view.Unsponsored);
        Assert.Equal(fixture.View.ManagerPersonId, view.ManagerPersonId);
        Assert.Equal(fixture.View.EmploymentStatusReason, view.EmploymentStatusReason);
    }

    [Fact]
    public async Task ShouldShowGapWithoutBlockingOtherChangesGivenSponsorLaterEnds()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedSponsorAsync("active");
        var before = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);

        // Act
        await fixture.ApplyObservationAsync(new WorkRelationshipEnded(fixture.Tenant,
            WorkRelationship.IdFor(fixture.Tenant, "SP-100"), "SP-100", 2, Start.AddMonths(1),
            "Sponsor left", Author, Now));
        var after = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);
        var changed = fixture.Owner.ChangeDepartment(1, "Finance", Author, Now);

        // Assert
        Assert.True(before.IsSuccess, before.Error?.Message);
        Assert.False(Assert.Single(before.Value).Unsponsored);
        Assert.True(after.IsSuccess, after.Error?.Message);
        Assert.True(Assert.Single(after.Value).Unsponsored);
        Assert.Null(changed);
    }

    [Fact]
    public async Task ShouldClearGapGivenAnotherActiveInternalRelationshipForSponsor()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedSponsorAsync("ended");
        var before = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);

        // Act
        await fixture.SeedSponsorAsync("active", workerId: "SP-200");
        var after = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);

        // Assert
        Assert.True(before.IsSuccess, before.Error?.Message);
        Assert.True(Assert.Single(before.Value).Unsponsored);
        Assert.True(after.IsSuccess, after.Error?.Message);
        Assert.False(Assert.Single(after.Value).Unsponsored);
    }

    [Fact]
    public async Task ShouldRetainGapGivenEndedExternalRelationshipWithoutActiveSponsor()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("ended");

        // Act
        var result = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(Assert.Single(result.Value).Unsponsored);
    }

    [Fact]
    public async Task ShouldExcludeOrdinaryWorkersGivenNoSponsor()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var ordinary = fixture.View with { WorkerType = "employee", SponsorPersonId = null, Unsponsored = true };

        // Act
        var result = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [ordinary], CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(Assert.Single(result.Value).Unsponsored);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenSponsorProjectionBehindSource()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var pending = DomainEventSeed.Attach(new WorkRelationshipRecorded(fixture.Tenant,
            WorkRelationship.IdFor(fixture.Tenant, "SP-100"), fixture.Sponsor, "SP-100",
            new WorkRelationshipTerms("employee", "active", Start, null, null, null, null), Author, Now),
            WorkRelationship.IdFor(fixture.Tenant, "SP-100"), 1);
        await fixture.Source.AppendAsync(new WorkRelationship(fixture.Tenant, pending.RelationshipId).Stream, 0, [pending]);

        // Act
        var result = await fixture.Sponsorship.EvaluateAsync(fixture.Tenant, [fixture.View], CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseForeignRelationshipGivenTenantMismatch()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var result = await fixture.Sponsorship.EvaluateAsync(Uuid.CreateVersion4(), [fixture.View], CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldShowGapWithoutPrivateFieldsGivenSingleOrListRead(bool list)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var consistency = new WorkRelationshipReadConsistency(fixture.Primary, new OwnerReader(fixture.Owner), fixture.Source);
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
            new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        WorkRelationshipView view;
        if (list)
        {
            var result = await new ListWorkRelationshipsHandler(fixture.Primary, consistency, fixture.Sponsorship)
                .HandleAsync(new RequestContext<ListWorkRelationships>(new(fixture.Tenant), actor), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            view = Assert.Single(result.Value.Items);
        }
        else
        {
            var result = await new GetWorkRelationshipHandler(consistency, new NoPrivateFields(), fixture.Sponsorship)
                .HandleAsync(new RequestContext<GetWorkRelationship>(new(fixture.Tenant, fixture.Owner.Id), actor), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            view = result.Value;
        }

        // Assert
        Assert.True(view.Unsponsored);
        Assert.Null(view.ManagerPersonId);
        Assert.Null(view.EmploymentStatusReason);
        Assert.True(view.RestrictedFieldsRedacted);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly CassieRepositoryTestDatabase _database;

        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Person { get; } = Uuid.CreateVersion4();
        public Uuid Sponsor { get; } = Uuid.CreateVersion4();
        public WorkRelationship Owner { get; private set; } = null!;
        public WorkRelationshipView View { get; private set; } = null!;
        public CassieWorkRelationshipDirectory Primary { get; }
        public CassieWorkforceObservationDirectory Observations { get; }
        public InMemoryEventStore Source { get; } = new();
        public WorkRelationshipSponsorship Sponsorship => new(Observations,
            new WorkforceObservationReadConsistency(Observations, Source));

        Fixture(CassieRepositoryTestDatabase database)
        {
            _database = database;
            Primary = new CassieWorkRelationshipDirectory(database.Database);
            Observations = new CassieWorkforceObservationDirectory(database.Database);
        }

        public async ValueTask DisposeAsync()
        {
            await Primary.DisposeAsync();
            await Observations.DisposeAsync();
            await _database.DisposeAsync();
        }

        public static async Task<Fixture> CreateAsync(string status = "active")
        {
            var fixture = new Fixture(await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements));
            fixture.Owner = new WorkRelationship(fixture.Tenant, WorkRelationship.IdFor(fixture.Tenant, "X-100"));
            var terms = new WorkRelationshipTerms("external_collaborator",
                status, Start, status == "ended" ? Start.AddMonths(1) : null, "Engineering", Uuid.CreateVersion4(),
                fixture.Sponsor, "Restricted workforce reason");
            WorkRelationshipRecorded fact;
            if (status is "pending" or "active")
            {
                Assert.True(fixture.Owner.Record(fixture.Person, "X-100", terms, Author, Now).IsSuccess);
                fact = Assert.IsType<WorkRelationshipRecorded>(
                    Assert.Single(new AggregateScenario<WorkRelationship>(fixture.Owner).PendingEvents));
            }
            else
            {
                fact = DomainEventSeed.Attach(new WorkRelationshipRecorded(fixture.Tenant, fixture.Owner.Id,
                    fixture.Person, "X-100", terms, Author, Now), fixture.Owner.Id, 1);
                fixture.Owner = NativeFacts.Replay(fixture.Owner, [fact]);
            }
            await fixture.ApplyPrimaryAsync(fact);
            await fixture.ApplyObservationAsync(fact);
            fixture.View = (await fixture.Primary.GetAsync(fixture.Tenant, fixture.Owner.Id))!;
            return fixture;
        }

        public Task SeedSponsorAsync(string status, string workerType = "employee", string workerId = "SP-100") =>
            ApplyObservationAsync(new WorkRelationshipRecorded(Tenant, WorkRelationship.IdFor(Tenant, workerId),
                Sponsor, workerId, new WorkRelationshipTerms(workerType, status, Start,
                    status == "ended" ? Start.AddMonths(1) : null, null, null,
                    workerType == "external_collaborator" ? Uuid.CreateVersion4() : null), Author, Now));

        async Task ApplyPrimaryAsync(DomainEvent fact)
        {
            await using var batch = await Primary.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("WorkRelationshipDirectoryV1",
                    EventStreamPattern.ForPattern(Tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
            await Primary.ApplyAsync(fact);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        public async Task ApplyObservationAsync(DomainEvent fact)
        {
            await using var batch = await Observations.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("WorkforceObservationsV1",
                    EventStreamPattern.ForPattern(Tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
            await Observations.ApplyAsync(fact);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
    }

    sealed class OwnerReader(WorkRelationship owner) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)(Aggregate)owner);
    }

    sealed class NoPrivateFields : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
