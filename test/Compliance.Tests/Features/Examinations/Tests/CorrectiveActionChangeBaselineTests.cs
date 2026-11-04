using System.Text.Json;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class CorrectiveActionChangeBaselineTests
{
    [Fact]
    public async Task ShouldCaptureEveryEditGivenCorrectiveActionChangedTwice()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        var first = CorrectiveActionChangeWorkflowTests.Change(fixture, finding) with { Description = "First correction." };
        await fixture.AsAsync(fixture.LeadUserId, first);
        await fixture.AsAsync(fixture.LeadUserId, first with
        {
            CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, Description = "Second correction.",
            OwnerMemberId = fixture.BackupMemberId, DueOn = fixture.Today.AddDays(6),
        });
        await using var scope = provider.CreateAsyncScope();
        var contributor = new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        using var capture = SnapshotSourceCapture.Begin(fixture.TenantId);

        // Act
        var result = await contributor.ReadAsync(Engagement(fixture), CancellationToken.None);
        var read = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(capture.TryGet(Assert.Single(result.Value.Sources).ContentSha256, out var material));
        using var envelope = JsonDocument.Parse(await File.ReadAllBytesAsync(material!.Path));
        var bytes = Convert.FromBase64String(envelope.RootElement.GetProperty("source_base64").GetString()!);
        var snapshot = JsonSerializer.Deserialize(bytes.AsSpan(Array.IndexOf(bytes, (byte)0) + 1),
            ComplianceCoreJsonContext.Default.FindingTypeIBaselineSourceContent)!;
        var changes = snapshot.History.Where(static fact => fact.Kind == "corrective_action_changed")
            .Select(fact => fact.Payload.Deserialize<CorrectiveActionChanged>(ComplianceCoreJsonContext.Default.Options)!).ToArray();
        Assert.Equal(["First correction.", "Second correction."], changes.Select(static fact => fact.Description));
        Assert.Equal([2L, 3L], changes.Select(static fact => fact.Revision));
        Assert.Equal(fixture.ReviewerMemberId, changes[0].OwnerMemberId);
        Assert.Equal(fixture.BackupMemberId, changes[1].OwnerMemberId);
        Assert.Equal(2, read.History.Count(static entry => entry.Change == "corrective_action_changed"));
        Assert.Equal("Second correction.", Assert.Single(read.CorrectiveActions).Description);
    }

    static ExaminationEngagementView Engagement(OperationsFixture fixture)
    {
        var date = fixture.Today.AddDays(1);
        return new ExaminationEngagementView(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), "type_i",
            "Audit Firm", date, null, ["security"], Uuid.CreateVersion4(), [], "draft", 1,
            new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), fixture.LeadMemberId, "Examiner");
    }
}
