using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PersonalWaiverApprovalTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldRequirePersonalApprovalGivenValidIndependentWaiver(string transport)
    {
        // Arrange
        await using var fixture = await WaiverManagementIndependenceTests.Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var before = await fixture.WaiverEventsAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        RequestInvocation invocation = transport == "http" ? new HttpInvocation("POST", "/synthetic/waiver/approve", "/synthetic/waiver/approve", "synthetic") :
            transport == "mcp" ? new McpInvocation("synthetic.waiver.approve") : new DirectInvocation();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ApproveSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId),
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.Approver), invocation), CancellationToken.None);

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.True(result.Value.Active);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver).ToString(), result.Value.Approver!.Id);
            Assert.Equal(fixture.Scope, result.Value.Scope);
            Assert.Equal(before + 1, await fixture.WaiverEventsAsync());
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before, await fixture.WaiverEventsAsync());
        }
    }
    [Theory]
    [InlineData("requester", RequestErrorKind.Forbidden, "A waiver must be approved by a different Org Admin who is not its beneficiary.")]
    [InlineData("beneficiary", RequestErrorKind.Forbidden, "A waiver must be approved by a different Org Admin who is not its beneficiary.")]
    [InlineData("grant", RequestErrorKind.Forbidden, "The actor may not manage this tenant's waivers.")]
    [InlineData("missing", RequestErrorKind.NotFound, "The separation-of-duties waiver was not found.")]
    [InlineData("expired", RequestErrorKind.Conflict, "The separation-of-duties waiver is outside its approval window.")]
    public async Task ShouldPreserveApprovalSourceRefusalGivenNativeHttp(string fault, RequestErrorKind kind, string message)
    {
        // Arrange
        await using var fixture = await WaiverManagementIndependenceTests.Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var waiverId = recorded.Value.WaiverId;
        if (fault == "missing")
            waiverId = Uuid.CreateVersion4();
        if (fault == "expired")
        {
            waiverId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.Tenant, waiverId), waiver =>
            {
                Assert.Null(waiver.Record(fixture.Scope, RbacIds.Member(fixture.Tenant, fixture.Beneficiary),
                    RbacIds.Member(fixture.Tenant, fixture.Requester), "Requester", "Historical exception", now.AddDays(-2), now.AddDays(-1)));
                return Result.Success;
            });
        }
        if (fault == "grant")
            fixture.Permissions.Allowed = false;
        var before = await fixture.WaiverEventsAsync();
        var user = fault == "requester" ? fixture.Requester : fault == "beneficiary" ? fixture.Beneficiary : fixture.Approver;

        // Act
        var result = await fixture.SendAsync(new ApproveSeparationOfDutiesWaiver(fixture.Tenant, waiverId), user);

        // Assert
        Assert.Equal(kind, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before, await fixture.WaiverEventsAsync());
    }

}
