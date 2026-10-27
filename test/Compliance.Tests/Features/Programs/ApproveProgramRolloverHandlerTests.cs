using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProgramRolloverAggregate = Bdgrz.Compliance.Features.Programs.Aggregates.ProgramRollover;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ApproveProgramRolloverHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldApproveRolloverGivenCurrentComplianceLeadProgramPermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var rolloverId = await fixture.SeedRolloverAsync();
        var handler = fixture.Handler(RbacPermissions.ProgramManage);
        var request = new ApproveProgramRollover(fixture.TenantId, fixture.ProgramId,
            rolloverId, 1, "approved", "Reviewed the completed period and carry-forward plan.");

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, rolloverId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var approval = Assert.Single(saved.Current!.Approvals);
        Assert.Equal(fixture.ActorMemberId, approval.ApprovedByMemberId);
        Assert.Equal("compliance_lead", approval.ApproverRole);
        Assert.Equal("approved", saved.Current.Status);
    }

    [Fact]
    public async Task ShouldRejectClaimedExecutiveRoleGivenNoPersistedApprovalPermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var rolloverId = await fixture.SeedRolloverAsync();
        var handler = fixture.Handler();
        var request = new ApproveProgramRollover(fixture.TenantId, fixture.ProgramId,
            rolloverId, 1, "approved", "A token role does not establish decision authority.");

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, request, "ceo"),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, rolloverId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectAmbiguousExecutiveAuthorityGivenCeoAndCisoPermissions()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var rolloverId = await fixture.SeedRolloverAsync();
        var handler = fixture.Handler(RbacPermissions.ProgramManage,
            RbacPermissions.ManagementExecutiveCeo, RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveProgramRollover(fixture.TenantId, fixture.ProgramId,
            rolloverId, 1, "approved", "The actor holds conflicting executive authorities.");

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, rolloverId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldTreatSameApprovalRetryAsIdempotentGivenLaterClockTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var rolloverId = await fixture.SeedRolloverAsync();
        var handler = fixture.Handler(RbacPermissions.ManagementExecutiveCiso,
            RbacPermissions.ProgramManage);
        var request = new ApproveProgramRollover(fixture.TenantId, fixture.ProgramId,
            rolloverId, 1, "approved", "Reviewed the completed period and carry-forward plan.");
        var context = Fixture.Context(fixture.UserId, request);
        Assert.True((await handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        fixture.Clock.Set(Now.AddHours(3));

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, rolloverId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, saved.Revision);
        var approval = Assert.Single(saved.Current!.Approvals);
        Assert.Equal(Now, approval.ApprovedAt);
        Assert.Equal("ciso", approval.ApproverRole);
    }

    [Fact]
    public async Task ShouldRejectFirmStaffGivenCurrentProgramPermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync("firm_staff");
        var rolloverId = await fixture.SeedRolloverAsync();
        var handler = fixture.Handler(RbacPermissions.ProgramManage);
        var request = new ApproveProgramRollover(fixture.TenantId, fixture.ProgramId,
            rolloverId, 1, "approved", "Firm staff cannot approve for management.");

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, rolloverId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly MutableTimeProvider _clock = new(Now);

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ActorMemberId => RbacIds.Member(TenantId, UserId);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public MutableTimeProvider Clock => _clock;

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public ApproveProgramRolloverHandler Handler(params string[] permissions) =>
            new(Executor, Reader, new ProgramRolloverApproverAuthority(Reader,
                new GrantedPermissions(permissions)), _clock);

        public async Task RegisterActorAsync(string affiliation = "client_personnel")
        {
            var member = new Member(TenantId, UserId);
            Assert.True(member.Register(affiliation).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<Uuid> SeedRolloverAsync()
        {
            var rolloverId = Uuid.CreateVersion4();
            var proposerId = Uuid.CreateVersion4();
            var examinationId = Uuid.CreateVersion4();
            var snapshot = new TypeIIPeriodCloseSnapshotView(Uuid.CreateVersion4(), 1,
                [], [], [], Hash('a'), null, null, [], [], proposerId, "compliance_lead", "Proposer", Now);
            var sourcePeriod = new TypeIIPeriodView(TenantId, ProgramId, Uuid.CreateVersion4(),
                "closed", 4, examinationId, Uuid.CreateVersion4(), 2, Hash('b'),
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Uuid.CreateVersion4(), Hash('c'),
                [], [], [], [], null, [], [snapshot], proposerId, "Proposer", Now);
            var outcomeId = Uuid.CreateVersion4();
            var outcome = new AuditOutcomeView(TenantId, ProgramId, outcomeId,
                new AuditOutcomeContent(examinationId, "type_ii", snapshot.CloseSnapshotId,
                    snapshot.ContentSha256, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(),
                    Hash('d'), "auditor", "unmodified", "No material modifications were reported.",
                    "external-report-42", null, null, new DateOnly(2026, 10, 1)), "recorded", 1,
                proposerId, "Proposer", Now, Now, Hash('e'));
            var content = new ProgramRolloverContent(sourcePeriod.PeriodId, examinationId,
                snapshot.CloseSnapshotId, snapshot.ContentSha256, outcomeId, outcome.ContentSha256,
                new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31), Uuid.CreateVersion4(),
                Uuid.CreateVersion4(),
                [new ProgramRolloverRecordContent("control", Uuid.CreateVersion4(), 1,
                    Hash('f'), "carried", Uuid.CreateVersion4(), "Still approved.")], [],
                ["readiness"])
            {
                ProposalRationale = "Carry forward the reviewed control into the next period.",
            };
            var rollover = new ProgramRolloverAggregate(TenantId, ProgramId, rolloverId);
            Assert.Null(rollover.Propose(content, sourcePeriod, outcome, proposerId, "Proposer", Now));
            await Writer.SaveAsync(rollover, new RequestDispatchContext(RequestActor.System));
            return rolloverId;
        }

        public static Cntryl.Portia.RequestContext<ApproveProgramRollover> Context(Uuid userId,
            ApproveProgramRollover request, string? roleClaim = null)
        {
            var claims = new List<Claim>
            {
                new("iss", "bdgrz"),
                new("sub", userId.ToString()),
                new("email", "reviewer@example.test"),
            };
            if (roleClaim is not null)
                claims.Add(new Claim(ClaimTypes.Role, roleClaim));
            var actor = new ClaimsPrincipal(new ClaimsIdentity(claims, "BdgrzSession"));
            return new Cntryl.Portia.RequestContext<ApproveProgramRollover>(request,
                new RequestDispatchContext(actor, new HttpInvocation("POST",
                    "/api/v1/program-rollovers/approve", "/api/v1/program-rollovers/approve",
                    "approve-program-rollover-test")));
        }

        public ValueTask DisposeAsync() => DisposeCoreAsync();

        async ValueTask DisposeCoreAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }

        static string Hash(char value) => new(value, 64);
    }

    sealed class GrantedPermissions(params string[] permissions) : IAccessGrantPermissionAuthorizer
    {
        readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.Ordinal);

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(_permissions.Contains(permission));

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(false, new HashSet<Uuid>()));
    }

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Set(DateTimeOffset value) => _utcNow = value;
    }

    static string Hash(char value) => new(value, 64);
}
