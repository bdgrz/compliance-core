using System.Text.Json;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkAssignmentSourceReplayTests
{
    [Fact]
    public async Task ShouldReplayOriginalAssignmentGivenOriginalSourceWorkNowCompleted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var command = Uuid.CreateVersion4();
        var json = $$"""{"command_id":"{{command}}","tenant_id":"{{fixture.TenantId}}","program_id":"{{fixture.ProgramId}}","work_item_id":"{{item.WorkItemId}}","expected_revision":0,"assignee_member_id":"{{fixture.BackupMemberId}}","reason":"Original accountable member."}""";
        var request = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.AssignWorkItem)!;
        var original = await fixture.AsAsync(fixture.LeadUserId, request);
        var occurrence = (await fixture.OccurrencesAsync("missed")).Single(value => value.OccurrenceId == item.SourceId);
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var replay = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request,
            ProgramManagementServices.Actor(fixture.LeadUserId));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(original.Item, replay.Value.Item);
        Assert.Equal(original.History, replay.Value.History);
    }

    [Fact]
    public async Task ShouldExternalizeImportedHistoryGivenLargeReadOnlyLegacyLedger()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var events = new List<DomainEvent>();
        var actor = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        for (var index = 0; index < 256; index++)
            events.Add(DomainEventSeed.Attach(new WorkItemAssigned(fixture.TenantId, fixture.ProgramId,
                item.WorkItemId, index + 1, "reassign", fixture.OwnerMemberId, fixture.OwnerMemberId,
                new string('a', 2000), actor, DateTimeOffset.UtcNow), fixture.ProgramId, (ulong)index + 1));
        var legacy = new EventStreamAddress(fixture.TenantId.ToString(), "work-assignments", fixture.ProgramId.ToString());
        await fixture.Provider.GetRequiredService<IEventStore>().AppendAsync(legacy, 0, events);
        var command = Uuid.CreateVersion4();
        var json = $$"""{"command_id":"{{command}}","tenant_id":"{{fixture.TenantId}}","program_id":"{{fixture.ProgramId}}","work_item_id":"{{item.WorkItemId}}","expected_revision":256,"assignee_member_id":"{{fixture.BackupMemberId}}","reason":"Preserve the exact existing history."}""";
        var request = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.DelegateWorkItem)!;
        var result = await fixture.AsAsync(fixture.OwnerUserId, request);
        var native = new List<DomainEvent>();
        await foreach (var value in fixture.Provider.GetRequiredService<IDomainEventReader>().ReadAsync(
            new EventStreamAddress(fixture.TenantId.ToString(), WorkItemAssignment.Area,
                WorkItemAssignment.IdFor(fixture.ProgramId, item.WorkItemId).ToString()), 0))
            native.Add(value.Event);

        // Act
        var imported = Assert.Single(native, value => value.GetType().Name.EndsWith("HistoryImported", StringComparison.Ordinal));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(imported, ComplianceCoreJsonContext.Default.GetTypeInfo(imported.GetType())!);

        // Assert
        Assert.True(bytes.Length < 409_600, $"The imported native fact retained {bytes.Length} bytes of cumulative history.");
        Assert.Equal(257, result.History.Count);
        Assert.Equal(1, result.History[0].Revision);
        Assert.Equal(257, result.History[^1].Revision);
    }
}
