using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AttestServiceEngagementManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid OtherTenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("create", "http")]
    [InlineData("create", "mcp")]
    [InlineData("amend", "http")]
    [InlineData("amend", "mcp")]
    [InlineData("close", "http")]
    [InlineData("close", "mcp")]
    [InlineData("evaluate", "http")]
    [InlineData("evaluate", "mcp")]
    [InlineData("propose", "http")]
    [InlineData("propose", "mcp")]
    [InlineData("withdraw", "http")]
    [InlineData("withdraw", "mcp")]
    [InlineData("record_service", "http")]
    [InlineData("record_service", "mcp")]
    [InlineData("revoke", "http")]
    [InlineData("revoke", "mcp")]
    public async Task ShouldDenyClientIndependenceWriteGivenRetainedSameTenantAttestHistoryWithoutAppending(
        string operation, string transport)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        var before = await TenantEventCountAsync(provider, Tenant);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var error = await DispatchAsync(scope.ServiceProvider, operation, transport);
        var after = await TenantEventCountAsync(provider, Tenant);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains("Attest", error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldAllowClientIndependenceWriteGivenOnlyAdvisoryOrOtherTenantAttestHistory(
        string practice, bool historyBelongsToTenant)
    {
        // Arrange
        await using var provider = Compose();
        var historyTenant = historyBelongsToTenant ? Tenant : OtherTenant;
        var prior = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(provider, historyTenant, User,
            revoked: true, practice);
        await FirmStaffFacts.RegisterAsync(provider, prior.StaffMemberId, User, prior.Practice,
            "Synthetic staff directory source",
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic directory operator"), Now);
        await using var scope = provider.CreateAsyncScope();
        var engagementId = Uuid.CreateVersion4();
        var request = new CreateServiceEngagement(Tenant, engagementId, 0,
            new ServiceEngagementDraftContent(prior.Practice, "Synthetic service scope", new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), prior.StaffMemberId));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context("create", "http"), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ServiceEngagementIndependence(Tenant, engagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1UL, retained.Revision);
        Assert.Equal("Synthetic service scope", result.Value!.Content.Scope);
    }

    [Fact]
    public async Task ShouldPreserveClientManagementGrantRequirementGivenNoAttestHistory()
    {
        // Arrange
        await using var provider = Compose(permitted: false);
        await using var scope = provider.CreateAsyncScope();
        var before = await TenantEventCountAsync(provider, Tenant);

        // Act
        var error = await DispatchAsync(scope.ServiceProvider, "create", "http");
        var after = await TenantEventCountAsync(provider, Tenant);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.DoesNotContain("Attest", error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ShouldMarkOnlyClientOwnedIndependenceMutationsForAttestManagementWallGivenRequestTypes()
    {
        // Arrange
        var requests = new[]
        {
            typeof(CreateServiceEngagement),
            typeof(AmendServiceEngagement),
            typeof(CloseServiceEngagement),
            typeof(EvaluateClientIndependence),
            typeof(ProposeServiceEngagementStaff),
            typeof(WithdrawServiceEngagementStaffProposal),
            typeof(RecordNonattestService),
            typeof(RevokeServiceEngagementActualStaff),
            typeof(AcknowledgeEngagementManagement),
        };

        // Act
        var marked = requests.Where(type => typeof(IClientManagementMutationRequest).IsAssignableFrom(type)).ToArray();

        // Assert
        Assert.Equal(requests.OrderBy(type => type.FullName), marked.OrderBy(type => type.FullName));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(AcceptServiceEngagement)));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(ReviseIndependenceRules)));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(GetServiceEngagement)));
    }

    static async Task<RequestError?> DispatchAsync(IServiceProvider services, string operation, string transport)
    {
        var bus = services.GetRequiredService<IRequestBus>();
        var context = Context(operation, transport);
        return operation switch
        {
            "create" => (await bus.DispatchAsync(new CreateServiceEngagement(Tenant, Uuid.CreateVersion4(), 0,
                Content), context, CancellationToken.None)).Error,
            "amend" => (await bus.DispatchAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), Tenant,
                Uuid.CreateVersion4(), 0, Content), context, CancellationToken.None)).Error,
            "close" => (await bus.DispatchAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), Tenant,
                Uuid.CreateVersion4(), 0, "Synthetic close reason"), context, CancellationToken.None)).Error,
            "evaluate" => (await bus.DispatchAsync(new EvaluateClientIndependence(Tenant, Uuid.CreateVersion4(), 1,
                new DateOnly(2026, 1, 1), 0), context, CancellationToken.None)).Error,
            "propose" => (await bus.DispatchAsync(new ProposeServiceEngagementStaff(Uuid.CreateVersion4(), Tenant,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0), context, CancellationToken.None)).Error,
            "withdraw" => (await bus.DispatchAsync(new WithdrawServiceEngagementStaffProposal(Uuid.CreateVersion4(),
                Tenant, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, "Synthetic withdrawal reason"), context,
                CancellationToken.None)).Error,
            "record_service" => (await bus.DispatchAsync(new RecordNonattestService(Tenant, Uuid.CreateVersion4(), 0,
                new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2026, 1, 1), null,
                    [], false, "Synthetic service source")), context, CancellationToken.None)).Error,
            "revoke" => (await bus.DispatchAsync(new RevokeServiceEngagementActualStaff(Uuid.CreateVersion4(), Tenant,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, "Synthetic revocation reason"), context,
                CancellationToken.None)).Error,
            _ => throw new InvalidOperationException(operation),
        };
    }

    static ServiceEngagementDraftContent Content => new("advisory", "Synthetic scope", new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31), Uuid.CreateVersion4());

    static RequestDispatchContext Context(string operation, string transport) => new(
        ProgramManagementServices.Actor(User), transport == "http"
            ? new HttpInvocation("POST", $"/synthetic/{operation}", $"/synthetic/{operation}", operation)
            : new McpInvocation($"bdgrz.service_engagement.{operation}"));

    static async Task<long> TenantEventCountAsync(IServiceProvider provider, Uuid tenant)
    {
        long count = 0;
        var events = provider.GetRequiredService<IDomainEventReader>();
        await foreach (var _ in events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString()), EventCursor.Start,
            CancellationToken.None))
            count++;
        return count;
    }

    static ServiceProvider Compose(bool permitted = true)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(permitted));
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(permitted)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(User, Tenant, "client_personnel") : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == Tenant.ToString() && userId == User);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
