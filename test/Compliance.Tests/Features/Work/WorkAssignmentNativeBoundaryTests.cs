using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

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
