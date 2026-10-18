using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Resolves selected package records against their immutable Type I baseline.</summary>
public sealed class AuditPackageSourceBuilder(IAggregateReader reader)
{
    public async ValueTask<Result<AuditPackageContent>> ResolveAsync(Uuid tenantId, Uuid programId,
        AuditPackageContent requested, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || requested is null ||
            requested.ExaminationId == Uuid.Empty)
            return Invalid("An audit package requires tenant, program, and examination identities.");

        if (requested.PackageKind != "type_i" || requested.SourceSnapshotKind != "type_i_baseline")
            return Conflict("Type II package creation requires a frozen close-snapshot resolver.");

        var engagement = await reader.HydrateAsync(new ExaminationEngagement(tenantId, programId,
            requested.ExaminationId), ct).ConfigureAwait(false);
        if (engagement.Current is null || engagement.Baseline is not { Status: "frozen" } baseline)
            return Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The frozen Type I baseline was not found in this program."));

        if (requested.SourceSnapshotId != baseline.TypeIBaselineId ||
            !StringComparer.OrdinalIgnoreCase.Equals(requested.SourceSnapshotSha256,
                baseline.ContentSha256))
            return Conflict("The package must identify the exact frozen Type I baseline.");

        if (requested.SelectedContent is null || requested.SelectedContent.Count is < 1 or > 256)
            return Invalid("An audit package requires a bounded explicit source selection.");

        foreach (var selected in requested.SelectedContent)
        {
            if (selected is null || !baseline.Sources.Any(source =>
                    source.Kind == selected.SourceType && source.RecordId == selected.SourceRecordId &&
                    source.Revision == selected.SourceVersion &&
                    StringComparer.OrdinalIgnoreCase.Equals(source.ContentSha256,
                        selected.ContentSha256)))
                return Conflict("Every selected package record must match an exact source version in the frozen Type I baseline.");
        }

        return Result<AuditPackageContent>.Success(requested);
    }

    static Result<AuditPackageContent> Invalid(string message) =>
        Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result<AuditPackageContent> Conflict(string message) =>
        Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
