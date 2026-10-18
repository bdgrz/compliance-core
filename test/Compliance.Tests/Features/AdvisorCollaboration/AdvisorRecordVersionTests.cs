using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorRecordVersionTests
{
    [Fact]
    public void ShouldPreserveNativeControlVersionAndRevisionGivenControlDraft()
    {
        // Arrange
        var controlId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();

        // Act
        var selected = AdvisorRecordVersion.ControlDraft(controlId, draftVersionId, 7);

        // Assert
        Assert.Equal("control_draft", selected.SourceKind);
        Assert.Equal(controlId, selected.RecordId);
        Assert.Equal(draftVersionId, selected.VersionId);
        Assert.Equal(7, selected.Revision);
        Assert.True(selected.HasValidIdentity);
    }

    [Fact]
    public void ShouldPreserveNativeNumericPolicyVersionGivenApprovedPolicy()
    {
        // Arrange
        var policyId = Uuid.CreateVersion4();

        // Act
        var selected = AdvisorRecordVersion.PolicyVersion(policyId, version: 12,
            revision: 19);

        // Assert
        Assert.Equal("policy_version", selected.SourceKind);
        Assert.Equal(policyId, selected.RecordId);
        Assert.Equal(Uuid.Empty, selected.VersionId);
        Assert.Equal(12, selected.VersionNumber);
        Assert.Equal(19, selected.Revision);
        Assert.True(selected.HasValidIdentity);
    }

    [Fact]
    public void ShouldPreserveEvidenceContentAndSourcePositionGivenEvidenceArtifact()
    {
        // Arrange
        var artifactId = Uuid.CreateVersion4();
        const string contentSha256 = "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881";

        // Act
        var selected = AdvisorRecordVersion.EvidenceArtifact(artifactId, revision: 3,
            sourcePosition: 5, contentSha256, evidenceSupportId: Uuid.CreateVersion4());

        // Assert
        Assert.Equal("evidence_artifact", selected.SourceKind);
        Assert.Equal(artifactId, selected.RecordId);
        Assert.Equal(Uuid.Empty, selected.VersionId);
        Assert.Equal(3, selected.Revision);
        Assert.Equal((ulong)5, selected.SourcePosition);
        Assert.Equal(contentSha256, selected.ContentSha256);
        Assert.NotEqual(Uuid.Empty, selected.EvidenceSupportId);
        Assert.True(selected.HasValidIdentity);
    }

    [Fact]
    public void ShouldRejectEvidenceArtifactWithoutProgramSupportIdentityGivenEvidenceArtifact()
    {
        // Arrange
        var selected = AdvisorRecordVersion.EvidenceArtifact(Uuid.CreateVersion4(), revision: 3,
            sourcePosition: 5,
            "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881",
            evidenceSupportId: Uuid.Empty);

        // Act
        var valid = selected.HasValidIdentity;

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void ShouldPinReadinessGapToAssessmentFingerprintGivenReadinessGap()
    {
        // Arrange
        var gapId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        const string fingerprint = "a4f7c33f90bb60b48d55e5fc80bde66920d41fb327fbd91424f24ba7a42a26d0";

        // Act
        var selected = AdvisorRecordVersion.ReadinessGap(gapId, assessmentId, fingerprint);

        // Assert
        Assert.Equal("readiness_gap", selected.SourceKind);
        Assert.Equal(gapId, selected.RecordId);
        Assert.Equal(Uuid.Empty, selected.VersionId);
        Assert.Equal(assessmentId, selected.AssessmentId);
        Assert.Equal(fingerprint, selected.InputFingerprint);
        Assert.True(selected.HasValidIdentity);
    }

    [Fact]
    public void ShouldPinBoundaryQuestionToExactBoundaryVersionGivenQuestionEntry()
    {
        // Arrange
        var boundaryId = Uuid.CreateVersion4();
        var entryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();

        // Act
        var selected = AdvisorRecordVersion.BoundaryQuestion(boundaryId, entryId,
            versionId, revision: 4);

        // Assert
        Assert.Equal("boundary_question", selected.SourceKind);
        Assert.Equal(boundaryId, selected.RecordId);
        Assert.Equal(entryId, selected.EntryId);
        Assert.Equal(versionId, selected.VersionId);
        Assert.Equal(4, selected.Revision);
        Assert.True(selected.HasValidIdentity);
    }

    [Fact]
    public void ShouldRejectIncompleteNativeVersionGivenPolicyVersion()
    {
        // Arrange
        var selected = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.Empty)
        {
            SourceKind = "policy_version",
            VersionNumber = 0,
            Revision = 2,
        };

        // Act
        var valid = selected.HasValidIdentity;

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void ShouldReplayLegacyUuidPairGivenNativeVersionFieldsAreAdded()
    {
        // Arrange
        var original = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var json = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.AdvisorRecordVersion);
        var replay = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.AdvisorRecordVersion)!;

        // Assert
        Assert.DoesNotContain("source_kind", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revision", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original.RecordId, replay.RecordId);
        Assert.Equal(original.VersionId, replay.VersionId);
        Assert.Equal(AdvisorRecordVersion.Legacy, replay.SourceFamily);
        Assert.True(replay.HasValidIdentity);
        Assert.False(replay.IsSupported);
    }

    [Fact]
    public void ShouldSerializePolicyVersionWithoutSyntheticUuidGivenNativeVersionIdentity()
    {
        // Arrange
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), version: 12,
            revision: 19);

        // Act
        var json = JsonSerializer.Serialize(selected,
            ComplianceCoreJsonContext.Default.AdvisorRecordVersion);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert
        Assert.Contains("source_kind", properties);
        Assert.Contains("version_number", properties);
        Assert.DoesNotContain("version_id", properties);
        var replay = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.AdvisorRecordVersion)!;
        Assert.Equal(selected, replay);
    }
}
