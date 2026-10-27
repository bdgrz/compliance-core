using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
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
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Registered firm staff assigned to engagements that were accepted straight on their owners, exactly as the
///     acceptance process leaves them, with the permanent practice walls committed. It drives the actual staff locator
///     projection and the directory reconciliation reactors over the production composition.
/// </summary>
sealed class AcceptedStaffFixture : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    readonly IndependenceHost _host = new();

    AcceptedStaffFixture()
    {
    }

    public Uuid Tenant => _host.Tenant;

    public IServiceProvider Provider => _host.Provider;

    public IServiceProvider Services => _host.Scope.ServiceProvider;

    public IReadOnlyList<FirmStaffMemberView> Staff { get; private set; } = [];

    public FirmStaffMemberView Lead => Staff[0];

    public CheckpointIdentity Identity => IdentityOf(Tenant);

    public static CheckpointIdentity IdentityOf(Uuid tenant) => new("ActualStaffEngagementLocatorV1",
        EventStreamPattern.ForPattern(tenant.ToString(), ServiceEngagementIndependence.Area));

    /// <summary>Registers <paramref name="staffCount" /> attest staff and accepts <paramref name="engagementCount" /> engagements assigning all of them.</summary>
    public static async Task<AcceptedStaffFixture> CreateAsync(int staffCount = 1, int engagementCount = 1)
    {
        var fixture = new AcceptedStaffFixture();
        var staff = new List<FirmStaffMemberView>();
        for (var index = 0; index < staffCount; index++)
            staff.Add(await fixture._host.RegisterStaffAsync("attest"));
        fixture.Staff = staff;
        for (var index = 0; index < engagementCount; index++)
            await fixture.AcceptAsync(fixture.Tenant, staff);
        return fixture;
    }

    /// <summary>Accepts one engagement of <paramref name="tenant" /> that assigns every staff member given.</summary>
    public async Task<(Uuid Engagement, Uuid Operation, ServiceEngagementAcceptanceView View)> AcceptAsync(Uuid tenant,
        IReadOnlyList<FirmStaffMemberView> staff, DateTimeOffset? at = null)
    {
        var when = at ?? Now;
        var partnerUser = Uuid.CreateVersion4();
        var partner = ActorReference.ForFirmStaff(partnerUser, "Synthetic partner");
        var client = ActorReference.ForMember(RbacIds.Member(tenant, Uuid.CreateVersion4()),
            "Synthetic client administrator");
        var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic ratified test rules ONLY"), staff[0].Actor, when, true);
        var engagement = Uuid.CreateVersion4();
        await ServiceEngagementFacts.CreateAsync(Provider, tenant, engagement, new ServiceEngagementDraftContent(
            "attest", "Synthetic scope", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), staff[0].StaffMemberId),
            staff[0], client, when);
        var view = new ServiceEngagementAcceptanceView(tenant, engagement, 1, 1, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), partnerUser, 1, 1, "Synthetic verified authority ONLY", null,
            Uuid.CreateVersion4(), null, null, null, null, new EngagementAcceptanceSourceTimes(when, when, null, null),
            rules, [], "allowed", "not_applicable", [],
            staff.Select(member => new EngagementActualAssignmentView(member.StaffMemberId, member.UserId,
                member.Practice, member.Revision, member.RecordedAt, true, partner, when,
                DateOnly.FromDateTime(when.UtcDateTime))).ToArray(), "active", partner, when);
        var operation = await ServiceEngagementFacts.AcceptAsync(Provider, tenant, engagement, view, partner, when);
        foreach (var member in staff)
            await PracticeCommitmentFacts.CommitAsync(Provider, tenant, engagement, operation, member, partner, when);
        return (engagement, operation, view);
    }

    /// <summary>Registers one more active attest staff member in the firm directory.</summary>
    public async Task<FirmStaffMemberView> AddStaffAsync() => await _host.RegisterStaffAsync("attest");

    /// <summary>Registers the tenant in the active tenant directory so the global reactors traverse it.</summary>
    public async Task RegisterTenantAsync(Uuid? tenantId = null)
    {
        var id = tenantId ?? Tenant;
        await ProgramManagementServices.SeedAsync(Provider, new Tenant(id), tenant =>
        {
            var slug = $"directory-{id.ToString()[..8]}";
            Assert.True(tenant.Register(Uuid.CreateVersion4(), "Synthetic registered client", slug).IsSuccess);
            return tenant.ConfirmSlug(slug);
        });
    }

    /// <summary>Changes the status of a staff member at its current revision.</summary>
    public Task<FirmStaffMemberView> SetStatusAsync(FirmStaffMemberView staff, bool active) =>
        FirmStaffFacts.SetStatusAsync(Provider, staff.StaffMemberId, active,
            "Synthetic operator status reason must stay out of client receipts",
            new DateTimeOffset(2026, 10, 8, 12, 1, 0, TimeSpan.Zero),
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Private synthetic status operator"));

    /// <summary>Runs the locator projection of <paramref name="tenant" /> to the end of its engagement facts.</summary>
    public async Task ProjectAsync(Uuid? tenant = null)
    {
        var id = tenant ?? Tenant;
        await using var scope = Provider.CreateAsyncScope();
        var registration = Assert.Single(scope.ServiceProvider.GetServices<WorkloadRegistration>(),
            item => item.Name == "ActualStaffEngagementLocatorV1");
        Assert.Equal(WorkloadScope.PerTenant, registration.Scope);
        var projector = (Projector)scope.ServiceProvider.GetRequiredService(registration.ComponentType);
        await new ProjectorScenario(new TenantId(id.ToString())).RunAsync(projector);
        var reader = scope.ServiceProvider.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var runner = new ProjectorRunner(scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
        var checkpoint = await reader.LoadCheckpointAsync(id);
        while (true)
        {
            var next = await runner.RunAsync(projector, checkpoint);
            if (next == checkpoint)
                return;
            checkpoint = next;
        }
    }

    /// <summary>Runs a reactor to the end of its source; a per-tenant reactor is bound to <paramref name="tenant" />.</summary>
    public async Task RunReactorAsync(string name, Uuid? tenant = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.Name == name);
        var reactor = (Reactor)services.GetRequiredService(registration.ComponentType);
        if (registration.Scope == WorkloadScope.PerTenant)
            await new ReactorScenario(new TenantId((tenant ?? Tenant).ToString())).RunAsync(reactor);
        var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
        var checkpoint = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
        var runner = new ReactorRunner(services.GetRequiredService<IDomainEventReader>(),
            services.GetRequiredService<IReactorPrincipalProvider>(), services.GetRequiredService<TimeProvider>());
        while (true)
        {
            var next = await runner.RunAsync(reactor, checkpoint);
            if (next == checkpoint)
                return;
            checkpoint = next;
        }
    }

    /// <summary>The checkpoint of a reactor workload.</summary>
    public async Task<ProjectionCheckpoint> CheckpointAsync(string name)
    {
        var registration = Assert.Single(Services.GetServices<WorkloadRegistration>(), item => item.Name == name);
        var reactor = (Reactor)Services.GetRequiredService(registration.ComponentType);
        return await Services.GetRequiredService<IProjectionCheckpointStore>()
            .LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
    }

    /// <summary>Every event record of the engagement streams of <paramref name="tenant" />, in order.</summary>
    public async Task<List<DomainEventRecord>> SourceRecordsAsync(Uuid? tenant = null)
    {
        var records = new List<DomainEventRecord>();
        try
        {
            await foreach (var record in Services.GetRequiredService<IDomainEventReader>().ReadAsync(
                               EventStreamPattern.ForPattern((tenant ?? Tenant).ToString(),
                                   ServiceEngagementIndependence.Area), EventCursor.Start, CancellationToken.None))
                records.Add(record);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten pattern this way.
        }

        return records;
    }

    /// <summary>The acceptance and staff assignment records of the engagement streams, in order.</summary>
    public async Task<List<DomainEventRecord>> AcceptanceRecordsAsync(Uuid? tenant = null) =>
        (await SourceRecordsAsync(tenant)).Where(record =>
            record.Event is ServiceEngagementAcceptanceRecorded or ServiceEngagementStaffAssigned).ToList();

    /// <summary>The receipts of every engagement of <paramref name="tenant" />, in stream order.</summary>
    public async Task<List<DirectoryIndependenceReevaluated>> ReceiptsAsync(Uuid? tenant = null) =>
        (await SourceRecordsAsync(tenant)).Select(record => record.Event)
        .OfType<DirectoryIndependenceReevaluated>().ToList();

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
