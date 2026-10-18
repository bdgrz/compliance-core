using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ApproveManagementAssertionHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldApproveExactAssertionGivenPersistedCisoPermissionAndSignerIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var assertionId = await fixture.SeedAssertionAsync(fixture.ActorMemberId, "ciso");
        var handler = fixture.Handler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveManagementAssertion(fixture.TenantId, fixture.ProgramId,
            assertionId, 1, "approve", "Reviewed the exact package and assertions.");

        // Act
        var result = await handler.HandleAsync(ContextFactory.Create(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertion(fixture.TenantId,
            fixture.ProgramId, assertionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var approval = Assert.Single(saved.Current!.Approvals);
        Assert.Equal(fixture.ActorMemberId, approval.ApprovedByMemberId);
        Assert.Equal("ciso", approval.ApproverRole);
        Assert.Equal("approved", saved.Current.Status);
    }

    [Fact]
    public async Task ShouldTreatApprovalRetryAsIdempotentGivenSameIntentAndLaterClockTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var assertionId = await fixture.SeedAssertionAsync(fixture.ActorMemberId, "ciso");
        var handler = fixture.Handler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveManagementAssertion(fixture.TenantId, fixture.ProgramId,
            assertionId, 1, "approve", "Reviewed the exact package and assertions.");
        var context = ContextFactory.Create(fixture.UserId, request);
        Assert.True((await handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        fixture.Clock.Set(Now.AddMinutes(2));

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertion(fixture.TenantId,
            fixture.ProgramId, assertionId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Single(saved.Current!.Approvals);
        Assert.Equal(Now.AddMinutes(1), saved.Current.Approvals[0].ApprovedAt);
    }

    [Fact]
    public async Task ShouldRejectClaimedExecutiveRoleGivenNoPersistedExecutivePermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var assertionId = await fixture.SeedAssertionAsync(fixture.ActorMemberId, "ciso");
        var handler = fixture.Handler();
        var request = new ApproveManagementAssertion(fixture.TenantId, fixture.ProgramId,
            assertionId, 1, "approve", "Claimed an executive role in the identity token.");

        // Act
        var result = await handler.HandleAsync(ContextFactory.Create(fixture.UserId, request,
            roleClaim: "ciso"), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertion(fixture.TenantId,
            fixture.ProgramId, assertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectGrantForDifferentSignerGivenManagementAssertion()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        var assertionId = await fixture.SeedAssertionAsync(Uuid.CreateVersion4(), "ciso");
        var handler = fixture.Handler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveManagementAssertion(fixture.TenantId, fixture.ProgramId,
            assertionId, 1, "approve", "The signer identity does not match this member.");

        // Act
        var result = await handler.HandleAsync(ContextFactory.Create(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertion(fixture.TenantId,
            fixture.ProgramId, assertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectFirmStaffGivenExecutivePermissionAndNamedSigner()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync("firm_staff");
        var assertionId = await fixture.SeedAssertionAsync(fixture.ActorMemberId, "ciso");
        var handler = fixture.Handler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveManagementAssertion(fixture.TenantId, fixture.ProgramId,
            assertionId, 1, "approve", "Firm staff cannot sign on management's behalf.");

        // Act
        var result = await handler.HandleAsync(ContextFactory.Create(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertion(fixture.TenantId,
            fixture.ProgramId, assertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSuspendedMemberGivenExecutivePermissionAndNamedSigner()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SuspendActorAsync();
        var assertionId = await fixture.SeedAssertionAsync(fixture.ActorMemberId, "ciso");
        var handler = fixture.Handler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveManagementAssertion(fixture.TenantId, fixture.ProgramId,
            assertionId, 1, "approve", "A suspended member cannot approve.");

        // Act
        var result = await handler.HandleAsync(ContextFactory.Create(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertion(fixture.TenantId,
            fixture.ProgramId, assertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(1, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly MutableTimeProvider _clock = new(Now.AddMinutes(1));

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
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
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public ApproveManagementAssertionHandler Handler(params string[] permissions) =>
            new(Executor, Reader, new ManagementExecutiveAuthority(Reader,
                new ExecutivePermissions(permissions)), _clock);

        public async Task RegisterActorAsync(string affiliation = "client_personnel")
        {
            var member = new Member(TenantId, UserId);
            Assert.True(member.Register(affiliation).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task SuspendActorAsync()
        {
            var member = await Reader.HydrateAsync(new Member(TenantId, UserId));
            Assert.True(member.Suspend(ActorMemberId, "Executive", Now, "Suspended for test.")
                .IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<Uuid> SeedAssertionAsync(Uuid signerId, params string[] signerRoles)
        {
            var assertionId = Uuid.CreateVersion4();
            var assertion = new ManagementAssertion(TenantId, ProgramId, assertionId);
            var content = new ManagementAssertionContent(ExaminationId, "type_i",
                Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(),
                new string('b', 64), Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(),
                new string('c', 64), "auditor", Uuid.CreateVersion4(), "Auditor-approved wording",
                [Reference("criteria"), Reference("control"), Reference("known_exception")],
                signerRoles.Select(role => new ManagementAssertionSigner(signerId, role)).ToArray());
            Assert.Null(assertion.Create(content, Uuid.CreateVersion4(), "Assertion author", Now));
            await Writer.SaveAsync(assertion, new RequestDispatchContext(RequestActor.System));
            return assertionId;
        }

        static ExaminationContentReference Reference(string kind) => new(kind,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('d', 64));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class ExecutivePermissions(params string[] permissions) : IAccessGrantPermissionAuthorizer
    {
        readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.Ordinal);

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(_permissions.Contains(permission));

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(false, new HashSet<Uuid>()));
    }

    static class ContextFactory
    {
        public static Cntryl.Portia.RequestContext<ApproveManagementAssertion> Create(Uuid userId,
            ApproveManagementAssertion request, string? roleClaim = null)
        {
            var claims = new List<Claim>
            {
                new("iss", "bdgrz"),
                new("sub", userId.ToString()),
                new("email", "executive@example.test"),
            };
            if (roleClaim is not null)
                claims.Add(new Claim(ClaimTypes.Role, roleClaim));
            var actor = new ClaimsPrincipal(new ClaimsIdentity(claims, "BdgrzSession"));
            return new Cntryl.Portia.RequestContext<ApproveManagementAssertion>(request,
                new RequestDispatchContext(actor, new HttpInvocation("POST",
                    "/api/v1/management-assertions/approve",
                    "/api/v1/management-assertions/approve", "assertion-approval-test")));
        }
    }

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;

        public void Set(DateTimeOffset value) => _utcNow = value;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
