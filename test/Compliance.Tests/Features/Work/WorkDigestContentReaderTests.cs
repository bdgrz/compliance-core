using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestContentReaderTests
{
    [Fact]
    public async Task ShouldSkipGuestDigestGivenScopedGuestMembership()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedProvider = fixture.Provider;
        var weekOf = fixture.Today.AddDays(-(((int)fixture.Today.DayOfWeek + 6) % 7));
        var visibility = new FixedProgramVisibilityAuthorizer(new ProgramAccessVisibility(false,
            new HashSet<Uuid> { fixture.ProgramId }));
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenant/{tenant_id}/{tenant_slug}/program/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        var dispatch = new WorkDigestDispatchStatusView(fixture.TenantId, fixture.OwnerMemberId,
            weekOf, "America/New_York", DateTimeOffset.UtcNow, "scheduled", 0, null,
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow, null, null);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = new WorkDigestContentReader(new TenantDirectory(fixture.TenantId),
            services.GetRequiredService<ITenantActivity>(),
            new MembershipDirectory(fixture.TenantId, fixture.OwnerUserId, "guest"),
            new EmailDirectory(fixture.OwnerUserId), new ProgramDirectory([]), visibility,
            services.GetRequiredService<WorkQueueReader>(),
            services.GetRequiredService<IAggregateReader>(), settings);

        // Act
        var result = await reader.ReadAsync(fixture.TenantId, fixture.OwnerUserId, dispatch,
            CancellationToken.None);

        // Assert
        Assert.Equal(WorkDigestContentReadKind.Skip, result.Kind);
        Assert.Equal("guest_member", result.SkipReason);
        Assert.Empty(visibility.Calls);
    }

    [Fact]
    public async Task ShouldOmitUnpermittedProgramWorkGivenCurrentDigestVisibility()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedProvider = fixture.Provider;
        var weekOf = fixture.Today.AddDays(-(((int)fixture.Today.DayOfWeek + 6) % 7));
        var permitted = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            weekOf.AddDays(2));
        var restrictedProgramId = Uuid.CreateVersion4();
        var restrictedSummary = "Restricted payroll review";
        var restrictedActionId = Uuid.CreateVersion4();
        var restrictedFindingId = Uuid.CreateVersion4();
        var recordedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var restrictedActor = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        await FindingSeed.RaiseAsync(fixture.Provider, fixture.TenantId, restrictedProgramId, restrictedFindingId,
            new FindingSource("manual", null, null, "Restricted payroll access issue."),
            "Restricted payroll finding", "Review payroll access", "high", "Payroll", fixture.OwnerMemberId,
            weekOf.AddDays(3), [], restrictedActor, recordedAt);
        await FindingSeed.AddActionAsync(fixture.Provider, fixture.TenantId, restrictedProgramId, restrictedFindingId,
            restrictedActionId, restrictedSummary, fixture.OwnerMemberId, weekOf.AddDays(3), restrictedActor,
            recordedAt.AddMinutes(1));
        var programs = new ProgramDirectory([
            Program(fixture.TenantId, fixture.ProgramId, "Security"),
            Program(fixture.TenantId, restrictedProgramId, "Payroll"),
        ]);
        var visibility = new FixedProgramVisibilityAuthorizer(new ProgramAccessVisibility(false,
            new HashSet<Uuid> { fixture.ProgramId }));
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenant/{tenant_id}/{tenant_slug}/program/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        var dispatch = new WorkDigestDispatchStatusView(fixture.TenantId, fixture.OwnerMemberId,
            weekOf, "America/New_York", DateTimeOffset.UtcNow, "scheduled", 0, null,
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow, null, null);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = new WorkDigestContentReader(new TenantDirectory(fixture.TenantId),
            services.GetRequiredService<ITenantActivity>(),
            services.GetRequiredService<ITenantMembershipDirectoryReader>(),
            new EmailDirectory(fixture.OwnerUserId), programs, visibility,
            services.GetRequiredService<WorkQueueReader>(),
            services.GetRequiredService<IAggregateReader>(), settings);

        // Act
        var result = await reader.ReadAsync(fixture.TenantId, fixture.OwnerUserId, dispatch,
            CancellationToken.None);

        // Assert
        Assert.Equal(WorkDigestContentReadKind.Ready, result.Kind);
        Assert.Contains(permitted.CorrectiveActions[0].Description, result.Message!.TextBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(restrictedSummary, result.Message.TextBody, StringComparison.Ordinal);
        Assert.Equal(fixture.TenantId, Assert.Single(visibility.Calls).TenantId);
        Assert.Equal(fixture.OwnerUserId, visibility.Calls[0].UserId);
        Assert.Equal(fixture.OwnerMemberId, visibility.Calls[0].MemberId);
        Assert.Equal(IProgramReadRequest.ReadPermission, visibility.Calls[0].Permission);
    }

    static ProgramView Program(Uuid tenantId, Uuid programId, string name) => new(tenantId,
        programId, name, "readiness", "type_i", 1,
        new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Owner",
        DateTimeOffset.UtcNow, []);

    sealed class TenantDirectory(Uuid tenantId) : ITenantDirectoryReader
    {
        public ValueTask<TenantView?> GetAsync(Uuid requestedTenantId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantView?>(
            requestedTenantId == tenantId
                ? new TenantView(tenantId, "Acme", "acme")
                : null);

        public ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantView>([new TenantView(tenantId, "Acme", "acme")],
                null));
    }

    sealed class EmailDirectory(Uuid userId) : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress,
            CancellationToken ct = default) => ValueTask.FromResult<EmailAddressView?>(null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid requestedUserId, int? limit,
            string? cursor, CancellationToken ct = default) => ValueTask.FromResult(
            new Page<EmailAddressView>(requestedUserId == userId
                ? [new EmailAddressView(userId, "owner@example.com", true)]
                : [], null));
    }

    sealed class MembershipDirectory(Uuid tenantId, Uuid userId, string affiliation)
        : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string requestedTenantId,
            Uuid requestedUserId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(requestedTenantId == tenantId.ToString() &&
                requestedUserId == userId
                    ? new TenantMembershipView(userId, tenantId, affiliation)
                    : null);

        public ValueTask<bool> IsMemberAsync(string requestedTenantId, Uuid requestedUserId,
            CancellationToken ct = default) => ValueTask.FromResult(
            requestedTenantId == tenantId.ToString() && requestedUserId == userId);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid requestedTenantId, int limit,
            string? cursor, CancellationToken ct = default) => ValueTask.FromResult(
            new Page<TenantMembershipView>(requestedTenantId == tenantId
                ? [new TenantMembershipView(userId, tenantId, affiliation)]
                : [], null));
    }

    sealed class ProgramDirectory(IReadOnlyList<ProgramView> programs) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult(programs.FirstOrDefault(item =>
            item.TenantId == tenantId && item.ProgramId == programId));

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>(
            programs.Where(item => item.TenantId == tenantId).Take(limit).ToArray(), null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class FixedProgramVisibilityAuthorizer(ProgramAccessVisibility visibility)
        : IAccessGrantPermissionAuthorizer
    {
        public List<(Uuid TenantId, Uuid UserId, Uuid MemberId, string Permission)> Calls { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(visibility.OrganizationWide || visibility.ProgramIds.Contains(programId));

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            Calls.Add((tenantId, userId, memberId, permission));
            return ValueTask.FromResult(visibility);
        }
    }
}
