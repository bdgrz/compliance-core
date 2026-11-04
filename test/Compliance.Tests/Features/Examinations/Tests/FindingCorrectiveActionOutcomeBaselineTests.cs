using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class FindingCorrectiveActionOutcomeBaselineTests
{
    [Fact]
    public async Task ShouldArchiveExactOutcomeSourceProofGivenFindingRecordsCancellation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await FindingCorrectiveActionOutcomeWorkflowTests.TerminalAsync(fixture, "cancelled");
        var report = FindingCorrectiveActionOutcomeWorkflowTests.Report(fixture, finding, source, "cancelled");
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(report).ExpectSuccess();
        await using var scope = provider.CreateAsyncScope();
        var contributor = new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        using var capture = SnapshotSourceCapture.Begin(fixture.TenantId);

        // Act
        var result = await contributor.ReadAsync(Engagement(fixture), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var selected = Assert.Single(result.Value.Sources);
        Assert.True(capture.TryGet(selected.ContentSha256, out var material));
        using var envelope = JsonDocument.Parse(await File.ReadAllBytesAsync(material!.Path));
        var bytes = Convert.FromBase64String(envelope.RootElement.GetProperty("source_base64").GetString()!);
        var payload = bytes.AsSpan(Array.IndexOf(bytes, (byte)0) + 1);
        var snapshot = JsonSerializer.Deserialize(payload, ComplianceCoreJsonContext.Default.FindingTypeIBaselineSourceContent)!;
        var recorded = Assert.Single(snapshot.History, fact => fact.Kind == "finding_corrective_action_resolved");
        var fact = recorded.Payload.Deserialize<FindingCorrectiveActionResolved>(ComplianceCoreJsonContext.Default.Options)!;
        Assert.Equal(report.Source, fact.Source);
        Assert.Equal("cancelled", fact.Outcome);
        Assert.Equal(finding.Revision + 1, selected.Revision);
        var proofBlob = Assert.Single(envelope.RootElement.GetProperty("content_blobs").EnumerateArray(), blob =>
            blob.GetProperty("reference").Deserialize<ImmutableContentRef>(ComplianceCoreJsonContext.Default.Options) == fact.SourceProof);
        var proofBytes = Convert.FromBase64String(proofBlob.GetProperty("base64").GetString()!);
        var proof = JsonSerializer.Deserialize<SourceFactLocator>(proofBytes, ComplianceCoreJsonContext.Default.Options)!;
        Assert.Equal(source.Event.Metadata.EventId, proof.EventId);
        Assert.Equal(source.Stream, proof.Source);
        Assert.Equal(source.ResourceOffset, proof.ResourcePosition);
    }

    static ExaminationEngagementView Engagement(OperationsFixture fixture)
    {
        var date = fixture.Today.AddDays(1);
        return new ExaminationEngagementView(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), "type_i",
            "Audit Firm", date, null, ["security"], Uuid.CreateVersion4(), [], "draft", 1,
            new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), fixture.LeadMemberId, "Examiner");
    }
}
