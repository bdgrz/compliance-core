using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class RiskAcceptanceWorkAuthorityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldHideAcceptanceGivenAcceptanceGrantWithoutSourceManagement(bool projected, bool executive)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        var userId = fixture.Operations.ReviewerUserId;
        var memberId = fixture.Operations.ReviewerMemberId;
        fixture.Permissions.Acceptors.Add((memberId, executive
            ? RbacPermissions.RiskAcceptExecutive : RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(executive);

        // Act
        await fixture.AcceptHttpAsync(fixture.Accept(pending, executive), userId, RequestErrorKind.Forbidden);
        var queue = await fixture.Scenario(userId).When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "unassigned")).ExpectSuccess();

        // Assert
        Assert.Empty(queue.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), queue.Value.Counts);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldDenyGuestRiskAcceptanceAndQueueGivenSourceGrants(bool projected, bool executive)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        fixture.Permissions.Managers.Add(fixture.GuestMemberId);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, executive
            ? RbacPermissions.RiskAcceptExecutive : RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(executive);

        // Act
        var denied = await fixture.AcceptHttpAsync(fixture.Accept(pending, executive),
            fixture.GuestUserId, RequestErrorKind.Forbidden);
        var queue = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "unassigned")).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, queue.Error?.Kind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldInvalidateAssignedAcceptanceGivenCurrentSourceGrantRevoked(bool projected, bool management)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        fixture.Permissions.Managers.Add(fixture.GuestMemberId);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, RbacPermissions.RiskAcceptComplianceLead));
        fixture.Permissions.Acceptors.Add((fixture.Operations.ApproverMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(false);
        var before = await fixture.Scenario(fixture.Operations.ApproverUserId).When(new ListWork(
            fixture.Operations.TenantId, fixture.Operations.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        await fixture.Scenario(fixture.Operations.ApproverUserId).When(new AssignWorkItem(
            fixture.Operations.TenantId, fixture.Operations.ProgramId, item.WorkItemId, 0,
            fixture.GuestMemberId)).ExpectSuccess();
        if (management)
            fixture.Permissions.Managers.Remove(fixture.GuestMemberId);
        else
            fixture.Permissions.Acceptors.Remove((fixture.GuestMemberId, RbacPermissions.RiskAcceptComplianceLead));

        // Act
        await fixture.AcceptHttpAsync(fixture.Accept(pending, false), fixture.GuestUserId, RequestErrorKind.Forbidden);
        var after = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "mine")).ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario().When(new GetWorkItem(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, item.WorkItemId)).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, after.Error?.Kind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldRequireExecutiveGivenResidualAboveOrWithoutAppetite(bool projected, bool unsetAppetite)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        fixture.Permissions.Managers.Add(fixture.GuestMemberId);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(!unsetAppetite, unsetAppetite ? null : 20);

        // Act
        await fixture.AcceptHttpAsync(fixture.Accept(pending, false), fixture.GuestUserId,
            RequestErrorKind.Forbidden);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, RbacPermissions.RiskAcceptExecutive));
        await fixture.AcceptHttpAsync(fixture.Accept(pending, true), fixture.GuestUserId,
            RequestErrorKind.Forbidden);
        var queue = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "unassigned")).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, queue.Error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyGuestQueueGivenGuestIsTheRiskAssessor(bool projected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        fixture.Permissions.Managers.Add(fixture.GuestMemberId);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(false, assessorMemberId: fixture.GuestMemberId);

        // Act
        await fixture.AcceptHttpAsync(fixture.Accept(pending, false), fixture.GuestUserId, RequestErrorKind.Forbidden);
        var queue = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "mine")).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, queue.Error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyGuestQueueGivenGuestOwnsRiskWithoutWaiver(bool projected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        fixture.Permissions.Managers.Add(fixture.GuestMemberId);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(false);
        var personId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(fixture.Operations.LeadMemberId, "Lead");
        await ProgramManagementServices.SeedAsync(fixture.Operations.Provider,
            new Person(fixture.Operations.TenantId, personId), person =>
                person.Record("Risk owner", null, actor, DateTimeOffset.UtcNow));
        await ProgramManagementServices.SeedAsync(fixture.Operations.Provider,
            new Person(fixture.Operations.TenantId, personId), person =>
            {
                Assert.Null(person.CorrelateMembership(1, fixture.GuestUserId, actor, DateTimeOffset.UtcNow));
                return Result.Success;
            });
        await RiskSeeds.AssignOwnerAsync(fixture.Operations.Provider, fixture.Operations.TenantId, fixture.Operations.ProgramId, pending.RiskId, personId, fixture.GuestMemberId, "Recorded risk accountability", actor, DateTimeOffset.UtcNow);

        // Act
        await fixture.AcceptHttpAsync(fixture.Accept(pending, false), fixture.GuestUserId, RequestErrorKind.Forbidden);
        var queue = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "mine")).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, queue.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyGuestRiskAcceptanceGivenProductionCompositionAndSourceGrants()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(false, productionComposition: true);
        fixture.Permissions.Managers.Add(fixture.GuestMemberId);
        fixture.Permissions.Acceptors.Add((fixture.GuestMemberId, RbacPermissions.RiskAcceptComplianceLead));
        var pending = await fixture.SeedAsync(false);

        // Act
        var denied = await fixture.AcceptHttpAsync(fixture.Accept(pending, false), fixture.GuestUserId,
            RequestErrorKind.Forbidden);
        var source = await ProgramManagementServices.HydrateAsync(fixture.Operations.Provider,
            new RiskEvaluation(fixture.Operations.TenantId, pending.RiskId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Empty(source.ToView().Acceptances);
    }

    sealed record Pending(Uuid RiskId, Uuid ResidualId);

    sealed class Fixture : IAsyncDisposable
    {
        public required OperationsFixture Operations { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required StrictPermissions Permissions { get; init; }
        public bool ProjectSource { get; init; }
        public required Uuid GuestUserId { get; init; }
        public Uuid GuestMemberId => Operations.Member(GuestUserId);

        public static async Task<Fixture> CreateAsync(bool projected, bool productionComposition = false)
        {
            var operations = await OperationsFixture.CreateAsync();
            var permissions = new StrictPermissions();
            permissions.Managers.Add(operations.ApproverMemberId);
            permissions.Managers.Add(operations.LeadMemberId);
            var guestUserId = Uuid.CreateVersion4();
            var guestMemberId = operations.Member(guestUserId);
            var events = operations.Provider.GetRequiredService<IEventStore>();
            DomainEvent registered = MemberFacts.Registered(operations.TenantId, guestMemberId,
                guestUserId, "guest", Uuid.CreateVersion4(),
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));
            registered.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), guestMemberId,
                1, DateTimeOffset.UtcNow));
            await events.AppendAsync(new EventStreamAddress(operations.TenantId.ToString(), "rbac-members",
                guestMemberId.ToString()), 0, [registered]);
            var client = new InMemoryKvClient();
            void Configure(IServiceCollection services)
            {
                services.AddSingleton(events);
                services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
                services.AddScoped<ITenantMembershipDirectoryReader, MembershipDirectory>();
                services.AddScoped<OperatingAuthority>();
                services.AddScoped<WorkQueueReader>();
                services.AddScoped<WorkQueueReadConsistency>();
                services.AddSingleton<IRiskDraftDirectoryReader>(operations.Risks);
                if (projected)
                {
                    services.AddScoped<FitzRiskEvaluationDirectory>(_ => new(client));
                    services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                        new RiskAcceptanceWorkItemDirectory(provider.GetRequiredService<IAggregateReader>(), provider.GetRequiredService<Bdgrz.Compliance.Features.Risks.RiskGovernanceReader>(), operations.Risks, null, provider.GetRequiredService<FitzRiskEvaluationDirectory>()));
                }
            }
            ServiceProvider provider;
            if (productionComposition)
            {
                var services = new ServiceCollection();
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                    ["Fitz:ApplicationName"] = "compliance"
                }).Build();
                _ = services.AddCompliance(configuration, developerAuthentication: true);
                services.AddSingleton<IPermissionAuthorizer>(permissions);
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                    new PermissionBackedAccessGrantPermissionAuthorizer(permissions));
                services.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
                services.AddSingleton<ITenantActivity, ActiveTenant>();
                Configure(services);
                provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            }
            else
            {
                provider = ProgramManagementServices.Build(permissions,
                    portia => portia.AddRequestHandler<AcceptRiskHandler>().AddRequestHandler<ListWorkHandler>()
                        .AddRequestHandler<GetWorkItemHandler>().AddRequestHandler<AssignWorkItemHandler>(), Configure);
            }
            return new Fixture
            {
                Operations = operations,
                Provider = provider,
                Permissions = permissions,
                ProjectSource = projected,
                GuestUserId = guestUserId
            };
        }

        public async Task<Result<RiskAcceptanceView>> AcceptHttpAsync(AcceptRisk request, Uuid userId,
            RequestErrorKind? expectedFailure = null)
        {
            await using var scope = Provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(userId),
                    new HttpInvocation("POST", "/synthetic/risk-acceptance", "/synthetic/risk-acceptance", "synthetic")),
                CancellationToken.None);
            if (expectedFailure is { } error)
                Assert.Equal(error, result.Error?.Kind);
            else
                Assert.True(result.IsSuccess, result.Error?.Message);
            return result;
        }

        public RequestScenario Scenario(Uuid? userId = null) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId ?? GuestUserId));

        public AcceptRisk Accept(Pending pending, bool executive) => new(Operations.TenantId,
            Operations.ProgramId, pending.RiskId, 3, pending.ResidualId,
            executive ? "executive" : "compliance_lead", DateTimeOffset.UtcNow.AddMonths(6),
            "Independently accepted within recorded authority.");

        public async Task<Pending> SeedAsync(bool executive, int? appetiteThreshold = 20, Uuid? assessorMemberId = null)
        {
            var now = DateTimeOffset.UtcNow.AddMinutes(-5);
            var riskId = Uuid.CreateVersion4();
            var residualId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(Operations.Provider,
                new RiskDraft(Operations.TenantId, riskId), risk => risk.Create(Operations.ProgramId,
                    Uuid.CreateVersion4(), "R-AUTH", new RiskDraftContent("Data exposure", "Customer data exposure",
                        "Loss of trust", null), Operations.LeadMemberId, "Lead", now));
            Operations.Risks.Add(new RiskDraftView(Operations.TenantId, Operations.ProgramId, riskId, "R-AUTH", 1,
                "assessed", "resolved", new RiskDraftContent("Data exposure", "Customer data exposure", "Loss of trust", null),
                Operations.LeadMemberId, "Lead", now));
            var method = await ProgramManagementServices.HydrateAsync(Operations.Provider,
                new RiskMethod(Operations.TenantId, Operations.ProgramId));
            if (method.Current is null)
            {
                await ProgramManagementServices.SeedAsync(Operations.Provider, method, source =>
                {
                    Assert.Null(source.Publish(Operations.ProgramId, 0,
                        ["rare", "unlikely", "possible", "likely", "almost certain"],
                        ["low", "minor", "moderate", "major", "severe"], appetiteThreshold,
                        ActorReference.ForMember(Operations.LeadMemberId, "Lead"), now));
                    return Result.Success;
                });
                method = await ProgramManagementServices.HydrateAsync(Operations.Provider,
                    new RiskMethod(Operations.TenantId, Operations.ProgramId));
            }
            await ProgramManagementServices.SeedAsync(Operations.Provider,
                new RiskEvaluation(Operations.TenantId, riskId), evaluation =>
                {
                    Assert.Null(evaluation.RecordAssessment(Operations.ProgramId, 0, Uuid.CreateVersion4(),
                        method.Current!, RiskEvaluation.Inherent, 2, 2, "Baseline exposure", Operations.LeadMemberId,
                        "Lead", now));
                    Assert.Null(evaluation.ChooseTreatment(Operations.ProgramId, 1, "accept", "Recorded treatment",
                        Operations.LeadMemberId, "Lead", now.AddMinutes(1)));
                    Assert.Null(evaluation.RecordAssessment(Operations.ProgramId, 2, residualId, method.Current!,
                        RiskEvaluation.Residual, executive ? 5 : 3, executive ? 5 : 3, "Residual exposure",
                        assessorMemberId ?? Operations.LeadMemberId, "Lead", now.AddMinutes(2)));
                    return Result.Success;
                });
            await CatchUpAsync();
            return new Pending(riskId, residualId);
        }

        public async Task CatchUpAsync()
        {
            if (!ProjectSource)
                return;
            await using var scope = Provider.CreateAsyncScope();
            var directory = scope.ServiceProvider.GetService<FitzRiskEvaluationDirectory>();
            if (directory is null)
                return;
            var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            var pattern = EventStreamPattern.ForPattern(Operations.TenantId.ToString(), "risk-evaluations");
            var identity = new CheckpointIdentity(FitzRiskEvaluationDirectory.ProjectorName, pattern);
            var checkpoint = await directory.LoadCheckpointAsync(Operations.TenantId);
            await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
            {
                await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, checkpoint));
                await directory.ApplyAsync(record.Event);
                checkpoint = new ProjectionCheckpoint(record.NextCursor);
                await batch.CommitAsync(checkpoint);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await Operations.Provider.DisposeAsync();
        }
    }

    sealed class StrictPermissions : IPermissionAuthorizer
    {
        public HashSet<Uuid> Managers { get; } = [];
        public HashSet<(Uuid MemberId, string Permission)> Acceptors { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission == IProgramReadRequest.ReadPermission ||
            permission == IProgramScopedRequest.ManagementPermission && Managers.Contains(memberId) ||
            Acceptors.Contains((memberId, permission)));
    }

    sealed class MembershipDirectory(IAggregateReader reader) : ITenantMembershipDirectoryReader
    {
        public async ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default)
        {
            var id = Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture);
            var member = await reader.HydrateAsync(new Member(id, userId), ct);
            return member.IsRegistered || member.IsDeprovisioned
                ? new TenantMembershipView(userId, id, member.Affiliation!, member.IsSuspended,
                    IsDeprovisioned: member.IsDeprovisioned)
                : null;
        }

        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => await GetAsync(tenantId, userId, ct) is
            { IsSuspended: false, IsDeprovisioned: false };

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
