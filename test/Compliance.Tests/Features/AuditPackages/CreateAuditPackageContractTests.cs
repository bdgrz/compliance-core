using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.AuditPackages;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class CreateAuditPackageContractTests
{
    [Fact]
    public void ShouldSerializeCompleteFrozenSelectionGivenCreateCommand()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var packageId = Uuid.CreateVersion4();
        var examinationId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var sourceVersionId = Uuid.CreateVersion4();
        var command = new CreateAuditPackage(tenantId, programId, packageId,
            new AuditPackageContent(examinationId, "type_i", "type_i_baseline",
                snapshotId, new string('a', 64), "SOC 2 Type I",
                [new AuditPackageSelectionEntry("system_description", "description-1",
                    "system_description", sourceId, 1, "json", new string('b', 64),
                    512, "management", sourceVersionId)]));

        // Act
        var json = JsonSerializer.Serialize(command,
            ComplianceCoreJsonContext.Default.CreateAuditPackage);
        var restored = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.CreateAuditPackage);

        // Assert
        Assert.Contains("type_i_baseline", json, StringComparison.Ordinal);
        Assert.Contains(sourceId.ToString(), json, StringComparison.Ordinal);
        Assert.NotNull(restored);
        Assert.Equal(command.TenantId, restored.TenantId);
        Assert.Equal(command.ProgramId, restored.ProgramId);
        Assert.Equal(command.PackageId, restored.PackageId);
        Assert.Equal(command.Content.ExaminationId, restored.Content.ExaminationId);
        Assert.Equal(command.Content.SourceSnapshotId, restored.Content.SourceSnapshotId);
        Assert.Equal(command.Content.SelectedContent, restored.Content.SelectedContent);
        Assert.Equal(sourceVersionId, Assert.Single(restored.Content.SelectedContent).SourceVersionId);
    }

    [Fact]
    public void ShouldSerializeTypeIIPeriodAndCloseSourceVersionGivenCreateCommand()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var packageId = Uuid.CreateVersion4();
        var examinationId = Uuid.CreateVersion4();
        var periodId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var sourceVersionId = Uuid.CreateVersion4();
        var command = new CreateAuditPackage(tenantId, programId, packageId,
            new AuditPackageContent(examinationId, "type_ii", "type_ii_close_snapshot",
                snapshotId, new string('a', 64), "SOC 2 Type II",
                [new AuditPackageSelectionEntry("control_matrix", "control-1", "control",
                    sourceId, 2, "json", new string('b', 64), 512, "management",
                    sourceVersionId)], periodId));

        // Act
        var json = JsonSerializer.Serialize(command,
            ComplianceCoreJsonContext.Default.CreateAuditPackage);
        var restored = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.CreateAuditPackage);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(periodId, restored.Content.SourcePeriodId);
        Assert.Equal(sourceVersionId, Assert.Single(restored.Content.SelectedContent).SourceVersionId);
    }
}
