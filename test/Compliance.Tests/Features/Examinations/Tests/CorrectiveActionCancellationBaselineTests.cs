using System.Security.Cryptography;
using System.Text.Json;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class CorrectiveActionCancellationBaselineTests
{
    [Fact]
    public async Task ShouldCaptureCancellationReasonAndActorGivenTypeIBaselineIncludesTerminalAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(4));
        var action = Assert.Single(finding.CorrectiveActions);
        await using var scope = provider.CreateAsyncScope();
        var contributor = new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        var engagement = Engagement(fixture);
        var before = await contributor.ReadAsync(engagement, CancellationToken.None);
        var reason = "Duplicate action; remediation tracked by the retained action.";
        await fixture.AsAsync(fixture.LeadUserId, new CancelCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, action.Revision, action.ActionId, reason));
        using var capture = SnapshotSourceCapture.Begin(fixture.TenantId);

        // Act
        var after = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(before.IsSuccess, before.Error?.Message);
        Assert.True(after.IsSuccess, after.Error?.Message);
        var source = Assert.Single(after.Value.Sources);
        Assert.NotEqual(Assert.Single(before.Value.Sources).ContentSha256, source.ContentSha256);
        Assert.True(capture.TryGet(source.ContentSha256, out var material));
        using var envelope = JsonDocument.Parse(await File.ReadAllBytesAsync(material!.Path));
        var bytes = Convert.FromBase64String(envelope.RootElement.GetProperty("source_base64").GetString()!);
        Assert.Equal(source.ContentSha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var separator = Array.IndexOf(bytes, (byte)0);
        var snapshot = JsonSerializer.Deserialize(bytes.AsSpan(separator + 1),
            ComplianceCoreJsonContext.Default.FindingTypeIBaselineSourceContent)!;
        var captured = Assert.Single(snapshot.History, fact => fact.Kind == "corrective_action_cancelled");
        var cancellation = captured.Payload.Deserialize<CorrectiveActionCancelled>(ComplianceCoreJsonContext.Default.Options)!;
        Assert.Equal(action.ActionId, cancellation.ActionId);
        Assert.Equal(reason, cancellation.Reason);
        Assert.Equal(fixture.LeadMemberId, cancellation.CancelledByMemberId);
        Assert.Equal(2, cancellation.Revision);
        Assert.DoesNotContain(snapshot.History, fact => fact.Kind == "corrective_action_completed");
        Assert.False(captured.Payload.TryGetProperty("evidence_manifest", out _));
    }

    static ExaminationEngagementView Engagement(OperationsFixture fixture)
    {
        var date = fixture.Today.AddDays(1);
        return new ExaminationEngagementView(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), "type_i",
            "Audit Firm", date, null, ["security"], Uuid.CreateVersion4(), [], "draft", 1,
            new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), fixture.LeadMemberId, "Examiner");
    }
}
