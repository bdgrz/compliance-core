using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Events;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class AccessReviewScopeLegacyAndProjectionTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    readonly IImmutableContentStore _content = new FitzKvImmutableContentStore(new InMemoryKvClient());

    [Theory]
    [InlineData("application")]
    [InlineData("actor")]
    [InlineData("content_digest")]
    public async Task ShouldRejectContradictoryScopeBodyGivenNativeDecisionSource(string fault)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var original = await DecidedAsync(tenant, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "included", Now);
        var changed = fault switch
        {
            "application" => original with { ApplicationId = Uuid.CreateVersion4() },
            "actor" => original with { Operation = original.Operation with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Other") } },
            "content_digest" => original with { Content = original.Content with { Sha256 = new string('f', 64) } },
            _ => original,
        };

        // Act
        var error = await Record.ExceptionAsync(async () => await AccessReviewScopeReader.ReadDecisionAsync(_content, changed));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
    }

    [Fact]
    public async Task ShouldRecordDecisionGivenLegacyApplicationStreamInstance()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(registrantId: Uuid.CreateVersion4());
        var decide = scenario.DecideHandler();
        var get = new GetAccessReviewScopeHandler(scenario.Reader, scenario.Events, scenario.Content,
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(scenario.Reader));

        // Act
        var decided = await decide.HandleAsync(scenario.DecideContext(Uuid.CreateVersion4()),
            CancellationToken.None);
        var read = await get.HandleAsync(new RequestContext<GetAccessReviewScope>(
            new GetAccessReviewScope(scenario.TenantId, scenario.ApplicationId,
                scenario.InstanceId, Now.AddDays(1)), Principal(Uuid.CreateVersion4())),
            CancellationToken.None);

        // Assert
        Assert.True(decided.IsSuccess);
        Assert.Equal(1, decided.Value.SystemInstanceRevision);
        Assert.True(read.IsSuccess);
        Assert.Equal("included", read.Value.Status);
    }

    [Fact]
    public async Task ShouldProhibitLegacyRegistrantGivenNoWaiver()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(registrantId: null, userId);
        var decide = scenario.DecideHandler();

        // Act
        var result = await decide.HandleAsync(scenario.DecideContext(userId),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldProhibitCorrelatedAccessOwnerGivenIndependentRegistrant(bool legacy)
    {
        // Arrange
        var ownerUserId = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(Uuid.CreateVersion4(),
            accessOwnerUserId: ownerUserId, legacy: legacy);

        // Act
        var result = await scenario.DecideHandler().HandleAsync(
            scenario.DecideContext(ownerUserId), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        var recorded = await scenario.ReadDecisionsAsync();
        Assert.Empty(recorded);
    }

    [Fact]
    public async Task ShouldRecordWaivedOwnerDecisionGivenExactScopeWaiver()
    {
        // Arrange
        var ownerUserId = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(Uuid.CreateVersion4(),
            accessOwnerUserId: ownerUserId);
        var waiverId = await scenario.RecordOwnerWaiverAsync(ownerUserId);

        // Act
        var result = await scenario.DecideHandler().HandleAsync(
            scenario.DecideContext(ownerUserId, waiverId), CancellationToken.None);
        var recorded = await scenario.ReadDecisionsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        var decision = Assert.Single(recorded);
        Assert.Equal(waiverId, decision.SeparationOfDutiesWaiverId);
        Assert.Equal(RbacIds.Member(scenario.TenantId, ownerUserId).ToString(), decision.ApprovedBy.Id);
    }

    [Fact]
    public async Task ShouldUseCurrentApplicationOwnerGivenReassignedOwnership()
    {
        // Arrange
        var priorOwner = Uuid.CreateVersion4();
        var currentOwner = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(Uuid.CreateVersion4(),
            accessOwnerUserId: priorOwner);
        var currentPerson = await scenario.RecordPersonAsync(currentOwner);
        await scenario.SetAccessOwnerAsync(currentPerson);

        // Act
        var denied = await scenario.DecideHandler().HandleAsync(
            scenario.DecideContext(currentOwner), CancellationToken.None);
        var allowed = await scenario.DecideHandler().HandleAsync(
            scenario.DecideContext(priorOwner), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(denied.Error).Kind);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task ShouldUseCurrentPersonCorrelationGivenRelinkedOwnerMembership()
    {
        // Arrange
        var priorMember = Uuid.CreateVersion4();
        var currentMember = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(Uuid.CreateVersion4(),
            accessOwnerUserId: priorMember);
        await scenario.CorrelateOwnerAsync(currentMember);

        // Act
        var denied = await scenario.DecideHandler().HandleAsync(
            scenario.DecideContext(currentMember), CancellationToken.None);
        var allowed = await scenario.DecideHandler().HandleAsync(
            scenario.DecideContext(priorMember), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(denied.Error).Kind);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task ShouldAllowIndependentApprovalGivenOwnerMembershipUnlinked()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(Uuid.CreateVersion4(),
            accessOwnerUserId: userId);
        await scenario.CorrelateOwnerAsync(null);

        // Act
        var result = await scenario.DecideHandler().HandleAsync(scenario.DecideContext(userId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldIgnoreForeignPersonCorrelationGivenSamePersonIdOutsideTenant()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(Uuid.CreateVersion4(),
            accessOwnerUserId: userId, ownerTenantId: Uuid.CreateVersion4());

        // Act
        var result = await scenario.DecideHandler().HandleAsync(scenario.DecideContext(userId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenLegacyInstanceOfAnotherApplication()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(registrantId: Uuid.CreateVersion4());
        var decide = scenario.DecideHandler();
        var context = new Cntryl.Portia.RequestContext<DecideAccessReviewScope>(new DecideAccessReviewScope(
            scenario.TenantId, Uuid.CreateVersion4(), scenario.InstanceId, 1, 0, "included",
            "Production", Now), PersonalHttp(Principal(Uuid.CreateVersion4())));

        // Act
        var result = await decide.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldProjectSupersedingHistoryIdempotentlyGivenReplayedDecisions()
    {
        // Arrange
        var directory = new FitzAccessReviewScopeDirectory(new InMemoryKvClient(), _content);
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var first = await DecidedAsync(tenantId, applicationId, instanceId, 1, "included", Now);
        var second = await DecidedAsync(tenantId, applicationId, instanceId, 2, "excluded", Now.AddDays(10));

        // Act
        await ProjectAsync(directory, tenantId, first, second, first, second);
        var record = await directory.GetAsync(tenantId, instanceId);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), instanceId);

        // Assert
        Assert.NotNull(record);
        Assert.Equal([1L, 2L], record.Decisions.Select(decision => decision.Sequence));
        Assert.Null(alien);
    }

    [Fact]
    public async Task ShouldRejectOutOfOrderDecisionGivenMissingPredecessor()
    {
        // Arrange
        var directory = new FitzAccessReviewScopeDirectory(new InMemoryKvClient(), _content);
        var tenantId = Uuid.CreateVersion4();
        var gap = await DecidedAsync(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2,
            "included", Now);

        // Act
        var failure = await Record.ExceptionAsync(() => ProjectAsync(directory, tenantId, gap));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void ShouldReportEffectiveUpcomingAndOverdueGivenDatedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var instance = new SystemInstanceView(tenantId, applicationId, instanceId, "AWS",
            "cloud_account", null, "manual", null, [], Uuid.CreateVersion4(), "Registrant", Now);
        var history = new[]
        {
            DecisionView(tenantId, applicationId, instanceId, 1, "included", Now, Now.AddDays(5)),
            DecisionView(tenantId, applicationId, instanceId, 2, "excluded", Now.AddDays(30)),
        };

        // Act
        var before = AccessReviewScopeStatus.Evaluate(instance, history, Now.AddDays(-1));
        var overdue = AccessReviewScopeStatus.Evaluate(instance, history, Now.AddDays(6));
        var later = AccessReviewScopeStatus.Evaluate(instance, history, Now.AddDays(31));

        // Assert
        Assert.Equal("unresolved", before.Status);
        Assert.Equal(1, before.Upcoming?.Sequence);
        Assert.Equal("included", overdue.Status);
        Assert.True(overdue.ReviewOverdue);
        Assert.Equal(2, overdue.Upcoming?.Sequence);
        Assert.Equal("excluded", later.Status);
        Assert.False(later.ReviewOverdue);
        Assert.Null(later.Upcoming);
    }

    static async Task ProjectAsync(FitzAccessReviewScopeDirectory directory, Uuid tenantId,
        params AccessReviewScopeDecided[] events)
    {
        var identity = new CheckpointIdentity(AccessReviewScopeStreams.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(),
                "system-instance-access-review-scopes"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var ev in events)
            await directory.ApplyAsync(ev);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    async Task<AccessReviewScopeDecided> DecidedAsync(Uuid tenantId, Uuid applicationId, Uuid instanceId,
        long sequence, string decision, DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy = null)
    {
        var view = DecisionView(tenantId, applicationId, instanceId, sequence, decision, effectiveFrom, reviewBy);
        var body = await NativeContent.WriteAsync(_content, tenantId, AccessReviewScopeReader.DecisionSchema, view);
        var owner = new SystemInstanceAccessReviewScope(tenantId, instanceId);
        var operation = RootOperation.Provenance(view.DecisionId, nameof(DecideAccessReviewScope), owner.Stream,
            view.ApprovedBy, sequence - 1, new string('d', 64), Now) with
        { FirstOwner = owner.Stream };
        return DomainEventSeed.Attach(new AccessReviewScopeDecided(tenantId, applicationId, instanceId,
            1, view.DecisionId, sequence, effectiveFrom, body, operation, NativeFacts.Ref(), NativeFacts.Ref()),
            instanceId, (ulong)sequence);
    }

    static AccessReviewScopeDecisionView DecisionView(Uuid tenantId, Uuid applicationId, Uuid instanceId,
        long sequence, string decision, DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy = null) =>
        new(tenantId, applicationId, instanceId, 1, Uuid.CreateVersion4(), sequence, decision, "Reason",
            effectiveFrom, reviewBy, ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now, null);

    static RequestDispatchContext PersonalHttp(ClaimsPrincipal actor) => new(actor,
        new HttpInvocation("POST", "/synthetic/scope/decision", "/synthetic/scope/decision", "synthetic"));

    static ClaimsPrincipal Principal(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        Scenario(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
        }

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ApplicationId { get; } = Uuid.CreateVersion4();
        public Uuid InstanceId { get; } = Uuid.CreateVersion4();
        public Uuid? AccessOwnerPersonId { get; private set; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();
        public IImmutableContentStore Content => _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        public Task<IReadOnlyList<AccessReviewScopeDecisionView>> ReadDecisionsAsync() =>
            AccessReviewScopeReader.ReadAsync(Reader, Events, Content, TenantId, ApplicationId, InstanceId);

        /// <summary>Declares a legacy instance; a null registrant means the member of <paramref name="userId"/>.</summary>
        public static async Task<Scenario> CreateAsync(Uuid? registrantId, Uuid? userId = null,
            Uuid? accessOwnerUserId = null, bool legacy = true, Uuid? ownerTenantId = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new InMemoryKvClient()));
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var scenario = new Scenario(services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }));
            var actor = registrantId ?? RbacIds.Member(scenario.TenantId, userId!.Value);
            Uuid? ownerPersonId = null;
            if (accessOwnerUserId is { } ownerUserId)
            {
                ownerPersonId = await scenario.RecordPersonAsync(ownerUserId, ownerTenantId);
                scenario.AccessOwnerPersonId = ownerPersonId;
            }
            var stream = new EventStreamAddress(scenario.TenantId.ToString(), "applications",
                scenario.ApplicationId.ToString());
            await scenario.Events.AppendAsync(stream, 0,
            [
                DomainEventSeed.Attach(new ApplicationDeclared(scenario.TenantId,
                    scenario.ApplicationId, "Payroll", "Run payroll", null, actor, "Manager",
                    Now, ApplicationFacts.Application(scenario.TenantId, scenario.ApplicationId), null, AccessOwnerPersonId: ownerPersonId), scenario.ApplicationId, 1),
            ]);
            if (legacy)
                await scenario.Events.AppendAsync(stream, 1,
                [DomainEventSeed.Attach(new SystemInstanceDeclared(scenario.TenantId,
                    scenario.ApplicationId, scenario.InstanceId, 2, "Production", "production",
                    null, "payroll-prod", actor, "Manager", Now), scenario.ApplicationId, 2)]);
            else
                await scenario.Events.AppendAsync(new EventStreamAddress(scenario.TenantId.ToString(),
                        "system-instances", scenario.InstanceId.ToString()), 0,
                [DomainEventSeed.Attach(new SystemInstanceRegistered(scenario.TenantId,
                    scenario.ApplicationId, scenario.InstanceId, 1, "Production", "production",
                    null, "payroll-prod", actor, "Manager", Now, ApplicationFacts.SystemInstance(scenario.TenantId, scenario.InstanceId), null), scenario.InstanceId, 1)]);
            return scenario;
        }

        public async Task<Uuid> RecordPersonAsync(Uuid userId, Uuid? tenantId = null)
        {
            var tenant = tenantId ?? TenantId;
            var personId = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Manager");
            await Events.AppendAsync(new EventStreamAddress(tenant.ToString(), "people",
                personId.ToString()), 0,
            [
                DomainEventSeed.Attach(new PersonRecorded(tenant, personId, "Access owner",
                    "owner@example.test", actor, Now), personId, 1),
                DomainEventSeed.Attach(new PersonMembershipCorrelated(tenant, personId, 2,
                    userId, actor, Now), personId, 2),
            ]);
            return personId;
        }

        public async Task SetAccessOwnerAsync(Uuid personId)
        {
            var application = await Reader.HydrateAsync(new DeclaredApplication(TenantId, ApplicationId));
            var revision = application.Revision + 1;
            await Events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "applications",
                ApplicationId.ToString()), (ulong)(revision - 1),
            [DomainEventSeed.Attach(new ApplicationRevised(TenantId, ApplicationId, revision,
                "Payroll", "Run payroll", null, Uuid.CreateVersion4(), "Manager", Now, ApplicationFacts.Application(TenantId, ApplicationId), null,
                AccessOwnerPersonId: personId), ApplicationId, (ulong)revision)]);
        }

        public async Task CorrelateOwnerAsync(Uuid? userId)
        {
            var personId = AccessOwnerPersonId!.Value;
            await Events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "people",
                personId.ToString()), 2,
            [DomainEventSeed.Attach(new PersonMembershipCorrelated(TenantId, personId, 3,
                userId, ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now), personId, 3)]);
        }

        public async Task<Uuid> RecordOwnerWaiverAsync(Uuid userId)
        {
            var waiverId = Uuid.CreateVersion4();
            var scope = new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.SystemInstanceAccessReviewScope,
                InstanceId, InstanceId, 1, SeparationOfDutiesActions.Approve);
            await Events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "separation-of-duties-waivers", waiverId.ToString()), 0,
            [
                DomainEventSeed.Attach(new SeparationOfDutiesWaiverRecorded(TenantId, waiverId,
                    scope, RbacIds.Member(TenantId, userId), Uuid.CreateVersion4(), "Admin",
                    "Small team", Now.AddDays(-1), Now.AddDays(1)), waiverId, 1),
                DomainEventSeed.Attach(new SeparationOfDutiesWaiverApproved(TenantId, waiverId,
                    Uuid.CreateVersion4(), "Second admin", Now), waiverId, 2),
            ]);
            return waiverId;
        }

        public DecideAccessReviewScopeHandler DecideHandler() => new(
            new NativeOperationExecutor(Reader, _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), Content),
            Reader, Events, Content, new FixedClock());

        public Cntryl.Portia.RequestContext<DecideAccessReviewScope> DecideContext(Uuid userId, Uuid? waiverId = null) => new(
            new DecideAccessReviewScope(TenantId, ApplicationId, InstanceId, 1, 0, "included",
                "Production data", Now, SeparationOfDutiesWaiverId: waiverId), PersonalHttp(Principal(userId)));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
