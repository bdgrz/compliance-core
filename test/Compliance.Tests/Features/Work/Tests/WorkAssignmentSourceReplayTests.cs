using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Queries;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

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
