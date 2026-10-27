using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class IndependenceAssignmentBoundaryTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid ClientUser = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("http")]
    [InlineData("mcp")]
    public async Task ShouldMapPersonPracticeConflictToPortiaConflictGivenAssignmentBoundary(string transport)
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", ClientUser.ToString())], "BdgrzSession"));

        // The retained synthetic acceptance and current directory intentionally disagree on practice.
        // This exercises the defensive history wall without asserting any firm rule-set values.
        var (conflictingStaff, priorEngagementId) = await AttestAssignmentHistoryFixture.SeedAcceptedAsync(provider,
            Tenant, Uuid.CreateVersion4());
        var operatorActor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        var newLead = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "advisory",
            "Synthetic current directory source", true, 1, operatorActor, Now);
        await FirmStaffFacts.RegisterAsync(provider, conflictingStaff.StaffMemberId, conflictingStaff.UserId,
            "advisory", "Synthetic current directory source", operatorActor, Now);
        await FirmStaffFacts.RegisterAsync(provider, newLead.StaffMemberId, newLead.UserId, "advisory",
            "Synthetic current directory source", operatorActor, Now);

        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var engagementId = Uuid.CreateVersion4();
        var engagementPath = $"/api/v1/tenants/{Tenant}/service-engagements/{engagementId}";
        var created = await bus.DispatchAsync(new CreateServiceEngagement(Tenant, engagementId, 0,
            new ServiceEngagementDraftContent("advisory", "Synthetic scope", new DateOnly(2026, 1, 1), null,
                newLead.StaffMemberId)), new RequestDispatchContext(actor,
            new HttpInvocation("PUT", $"{engagementPath}/create-service-engagement",
                $"{engagementPath}/create-service-engagement", "synthetic")), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var command = Uuid.CreateVersion4();
        RequestInvocation invocation = transport == "http"
            ? new HttpInvocation("PUT", $"{engagementPath}/propose-service-engagement-staff/{command}",
                $"{engagementPath}/propose-service-engagement-staff/{command}", "synthetic")
            : new McpInvocation("bdgrz.service-engagement.staff.propose");

        // Act
        var result = await bus.DispatchAsync(new ProposeServiceEngagementStaff(command, Tenant, engagementId,
            conflictingStaff.StaffMemberId, 1), new RequestDispatchContext(actor, invocation), CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(Tenant, engagementId), CancellationToken.None);
        var draft = await scope.ServiceProvider.GetRequiredService<ServiceEngagementViews>()
            .GetAsync(Tenant, engagementId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Contains(IndependenceCompartments.PersonExclusivityRuleId, result.Error!.Message,
            StringComparison.Ordinal);
        Assert.Contains(priorEngagementId.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(conflictingStaff.StaffMemberId.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(conflictingStaff.UserId.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1UL, retained.Revision);
        Assert.Equal("draft", draft!.Status);
        Assert.Equal(newLead.StaffMemberId, Assert.Single(draft.Staff).StaffMemberId);
    }
}
