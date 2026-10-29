using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Artifacts.Tests;
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
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Handlers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

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
