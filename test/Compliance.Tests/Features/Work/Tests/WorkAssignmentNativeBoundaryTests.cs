using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WorkAssignmentNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepCurrentAccountabilityGivenManyValidReassignments()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var first = fixture.BackupMemberId;
        var second = fixture.OwnerMemberId;
        for (var index = 0; index < 48; index++)
            await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                item.WorkItemId, index, index % 2 == 0 ? first : second, "Accountability changed."));
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        var retained = typeof(WorkItemAssignment).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is global::System.Collections.ICollection { Count: > 1 }).ToArray();
        var history = await scope.ServiceProvider.GetRequiredService<WorkAssignmentReader>().ReadAsync(owner);

        // Assert
        Assert.Empty(retained);
        Assert.Equal(48, owner.Revision);
        Assert.Equal(second, owner.AssigneeMemberId);
        Assert.Equal(48, history.State.History.Count);
    }

    [Fact]
    public async Task ShouldReturnOriginalAssignmentGivenExactSelectedCommandRetriedAfterLaterReassignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var command = Uuid.CreateVersion4();
        // Selected required identity is extra raw JSON so the old DTO is exercised without future APIs.
        var json = $$"""{"command_id":"{{command}}","tenant_id":"{{fixture.TenantId}}","program_id":"{{fixture.ProgramId}}","work_item_id":"{{item.WorkItemId}}","expected_revision":0,"assignee_member_id":"{{fixture.BackupMemberId}}","reason":"Original accountable member."}""";
        var request = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.AssignWorkItem)!;
        var original = await fixture.AsAsync(fixture.LeadUserId, request);
        await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, fixture.OwnerMemberId, "Owner returned."));
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var replay = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request,
            ProgramManagementServices.Actor(fixture.LeadUserId));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(original.Item, replay.Value.Item);
        Assert.Equal(original.History, replay.Value.History);
        var current = await fixture.AsAsync(fixture.LeadUserId,
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        Assert.Equal(fixture.OwnerMemberId, current.Item.AssigneeMemberId);
        Assert.Equal(2, current.History.Count);
    }
}
