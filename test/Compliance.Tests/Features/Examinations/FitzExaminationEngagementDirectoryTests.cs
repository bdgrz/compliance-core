using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class FitzExaminationEngagementDirectoryTests
{
    [Fact]
    public async Task ShouldProjectLatestEngagementRevisionGivenOpenedEngagement()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var engagementId = Uuid.CreateVersion4();
        var actorMemberId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var opened = new ExaminationEngagementView(tenantId, programId, engagementId,
            "type_i", "Original Audit LLP", new DateOnly(2026, 9, 30), null,
            ["security"], boundaryId, [], "draft", 1, now, actorMemberId, "Examiner");
        var revised = opened with
        {
            AuditFirm = "Updated Audit LLP",
            Revision = 2,
            UpdatedAt = now.AddMinutes(1),
        };
        var frozen = revised with
        {
            Status = "baseline_frozen",
            Revision = 3,
            UpdatedAt = now.AddMinutes(2),
        };
        var preview = TypeIBaselinePreviewTestFactory.Create(tenantId, programId, engagementId,
            [new TypeIBaselineSourceReference("approved_boundary", boundaryId,
                Uuid.CreateVersion4(), 1, new string('b', 64))]);
        var baseline = new TypeIBaselineView(tenantId, programId, engagementId,
            Uuid.CreateVersion4(), "frozen", 1, preview.Sources, [], preview.CanonicalManifest,
            preview.ContentSha256,
            frozen.UpdatedAt, actorMemberId, "Examiner", "Confirmed", null, null, [], [], 0)
        { SourceCoverage = preview.SourceCoverage };
        var amendedPreview = TypeIBaselinePreviewTestFactory.Create(tenantId, programId, engagementId,
            [new TypeIBaselineSourceReference("approved_boundary", boundaryId,
                Uuid.CreateVersion4(), 2, new string('c', 64))]);
        var amended = baseline with
        {
            TypeIBaselineId = Uuid.CreateVersion4(),
            Status = "amended",
            Revision = 2,
            Sources = amendedPreview.Sources,
            SourceCoverage = amendedPreview.SourceCoverage,
            CanonicalManifest = amendedPreview.CanonicalManifest,
            ContentSha256 = amendedPreview.ContentSha256,
            UpdatedAt = now.AddMinutes(3),
            ActorDisplay = "Approver",
            AmendsBaselineId = baseline.TypeIBaselineId,
            AmendmentReason = "Boundary scope correction",
            ChangedRecordIds = [boundaryId],
            AmendmentDepth = 1,
        };
        var amendedEngagement = frozen with
        {
            Revision = 4,
            UpdatedAt = amended.UpdatedAt,
            ActorDisplay = amended.ActorDisplay,
        };
        var directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzExaminationEngagementDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "examinations"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ExaminationEngagementOpened(tenantId, opened, now));
            await directory.ApplyAsync(new ExaminationEngagementRevised(tenantId, revised,
                revised.UpdatedAt));
            var frozenEvent = new TypeIBaselineFrozen(tenantId, frozen, baseline,
                frozen.UpdatedAt);
            await directory.ApplyAsync(frozenEvent);
            await directory.ApplyAsync(frozenEvent);
            var amendedEvent = new TypeIBaselineAmended(tenantId, amendedEngagement, amended,
                amended.UpdatedAt);
            await directory.ApplyAsync(amendedEvent);
            await directory.ApplyAsync(amendedEvent);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, engagementId);
        var otherTenant = await directory.GetAsync(Uuid.CreateVersion4(), engagementId);
        var frozenBaseline = await directory.GetBaselineAsync(tenantId, engagementId, baseline.TypeIBaselineId);
        var amendedBaseline = await directory.GetBaselineAsync(tenantId, engagementId, amended.TypeIBaselineId);
        var otherTenantBaseline = await directory.GetBaselineAsync(Uuid.CreateVersion4(), engagementId,
            baseline.TypeIBaselineId);

        // Assert
        Assert.Equal("Updated Audit LLP", current?.AuditFirm);
        Assert.Equal("baseline_frozen", current?.Status);
        Assert.Equal(4, current?.Revision);
        Assert.Null(otherTenant);
        Assert.Equal("frozen", frozenBaseline?.Status);
        Assert.Equal(0, frozenBaseline?.AmendmentDepth);
        Assert.Equal("amended", amendedBaseline?.Status);
        Assert.Equal(baseline.TypeIBaselineId, amendedBaseline?.AmendsBaselineId);
        Assert.Null(otherTenantBaseline);

        var malformedEvent = new TypeIBaselineAmended(tenantId, amendedEngagement, null!,
            amended.UpdatedAt);
        await using (var invalidBatch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                directory.ApplyAsync(malformedEvent).AsTask());

        var conflictingBaseline = baseline with { ConfirmationRationale = "Different rationale" };
        var conflictingEvent = new TypeIBaselineFrozen(tenantId, frozen, conflictingBaseline,
            frozen.UpdatedAt);
        await using var conflictBatch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            directory.ApplyAsync(conflictingEvent).AsTask());
    }
}
