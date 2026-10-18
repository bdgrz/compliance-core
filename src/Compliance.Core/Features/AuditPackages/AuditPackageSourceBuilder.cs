using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Resolves selected package records against their frozen Type I or Type II snapshot.</summary>
public sealed class AuditPackageSourceBuilder(IAggregateReader reader)
{
    public async ValueTask<Result<AuditPackageContent>> ResolveAsync(Uuid tenantId, Uuid programId,
        AuditPackageContent requested, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || requested is null ||
            requested.ExaminationId == Uuid.Empty)
            return Invalid("An audit package requires tenant, program, and examination identities.");

        if (requested.SelectedContent is null || requested.SelectedContent.Count is < 1 or > 256)
            return Invalid("An audit package requires a bounded explicit source selection.");

        Func<AuditPackageSelectionEntry, bool> isFrozenSource;
        if (requested.PackageKind == "type_i" && requested.SourceSnapshotKind == "type_i_baseline")
        {
            if (requested.SourcePeriodId != Uuid.Empty)
                return Invalid("A Type I package cannot identify a Type II period.");
            var engagement = await reader.HydrateAsync(new ExaminationEngagement(tenantId, programId,
                requested.ExaminationId), ct).ConfigureAwait(false);
            if (engagement.Current is null || engagement.Baseline is not { Status: "frozen" } baseline)
                return Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The frozen Type I baseline was not found in this program."));
            if (requested.SourceSnapshotId != baseline.TypeIBaselineId ||
                !StringComparer.OrdinalIgnoreCase.Equals(requested.SourceSnapshotSha256,
                    baseline.ContentSha256))
                return Conflict("The package must identify the exact frozen Type I baseline.");
            isFrozenSource = selected => baseline.Sources.Any(source =>
                source.Kind == selected.SourceType && source.RecordId == selected.SourceRecordId &&
                source.VersionId == selected.SourceVersionId &&
                source.Revision == selected.SourceVersion &&
                StringComparer.OrdinalIgnoreCase.Equals(source.ContentSha256,
                    selected.ContentSha256));
        }
        else if (requested.PackageKind == "type_ii" &&
                 requested.SourceSnapshotKind == "type_ii_close_snapshot")
        {
            if (requested.SourcePeriodId == Uuid.Empty)
                return Invalid("A Type II package must identify the period that owns its close snapshot.");
            var period = await reader.HydrateAsync(new TypeIIPeriod(tenantId, programId,
                requested.SourcePeriodId), ct).ConfigureAwait(false);
            if (period.Current is not { Status: "closed" } closed ||
                closed.CloseSnapshots.Count == 0)
                return Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The frozen Type II close snapshot was not found in this program."));
            if (closed.ExaminationId != requested.ExaminationId)
                return Conflict("The Type II period must belong to the requested examination.");
            var snapshot = closed.CloseSnapshots[^1];
            if (requested.SourceSnapshotId != snapshot.CloseSnapshotId ||
                !StringComparer.OrdinalIgnoreCase.Equals(requested.SourceSnapshotSha256,
                    snapshot.ContentSha256))
                return Conflict("The package must identify the latest frozen Type II close snapshot.");
            isFrozenSource = selected => snapshot.SourceReferences.Any(source =>
                source.SourceType == selected.SourceType && source.RecordId == selected.SourceRecordId &&
                source.VersionId == selected.SourceVersionId && source.Version == selected.SourceVersion &&
                StringComparer.OrdinalIgnoreCase.Equals(source.ContentSha256,
                    selected.ContentSha256));
        }
        else
            return Conflict("The package source type must match a supported frozen examination snapshot.");

        foreach (var selected in requested.SelectedContent)
        {
            if (selected is null || selected.SourceVersionId == Uuid.Empty || !isFrozenSource(selected))
                return Conflict("Every selected package record must match an exact source version in the frozen examination snapshot.");
        }

        return Result<AuditPackageContent>.Success(requested);
    }

    static Result<AuditPackageContent> Invalid(string message) =>
        Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result<AuditPackageContent> Conflict(string message) =>
        Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
