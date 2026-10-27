using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Resolves selected package records against their frozen Type I or Type II snapshot.</summary>
public sealed class AuditPackageSourceBuilder(ExaminationEngagementReader examinations, TypeIIPeriodReader periods, IImmutableContentStore? content = null)
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
        ImmutableContentRef? publication = null;
        if (requested.PackageKind == "type_i" && requested.SourceSnapshotKind == "type_i_baseline")
        {
            if (requested.SourcePeriodId != Uuid.Empty)
                return Invalid("A Type I package cannot identify a Type II period.");
            var engagement = await examinations.ReadAsync(tenantId, programId, requested.ExaminationId, ct).ConfigureAwait(false);
            if (engagement.View is null || engagement.Baseline is not { Status: "frozen" or "amended" } baseline)
                return Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The frozen Type I baseline was not found in this program."));
            if (requested.SourceSnapshotId != baseline.TypeIBaselineId ||
                !StringComparer.OrdinalIgnoreCase.Equals(requested.SourceSnapshotSha256,
                    baseline.ContentSha256))
                return Conflict("The package must identify the exact frozen Type I baseline.");
            publication = baseline.SourceContentManifest;
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
            var periodRead = await periods.TryReadAsync(tenantId, programId, requested.SourcePeriodId, ct).ConfigureAwait(false);
            if (!periodRead.IsSuccess)
                return Result<AuditPackageContent>.Failure(periodRead.Error!);
            var period = periodRead.Value;
            if (period.View is not { Status: "closed" } closed ||
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
            publication = snapshot.SourceContentManifest ?? closed.OpeningSourceContentManifest;
            isFrozenSource = selected => snapshot.SourceReferences.Any(source =>
                source.SourceType == selected.SourceType && source.RecordId == selected.SourceRecordId &&
                source.VersionId == selected.SourceVersionId && source.Version == selected.SourceVersion &&
                StringComparer.OrdinalIgnoreCase.Equals(source.ContentSha256,
                    selected.ContentSha256));
        }
        else
            return Conflict("The package source type must match a supported frozen examination snapshot.");

        SnapshotSourcePublicationManifest? published = null;
        if (publication is not null)
        {
            if (content is null || publication.Schema != SnapshotSourcePublicationManifest.Schema)
                return Conflict("The frozen source publication cannot be verified.");
            published = await SnapshotSourcePublicationReader.ReadAsync(content, tenantId, programId,
                requested.ExaminationId, publication, ct).ConfigureAwait(false);
        }
        var normalized = new List<AuditPackageSelectionEntry>(requested.SelectedContent.Count);
        foreach (var selected in requested.SelectedContent)
        {
            if (selected is null || selected.SourceVersionId == Uuid.Empty || !isFrozenSource(selected))
                return Conflict("Every selected package record must match an exact source version in the frozen examination snapshot.");
            var physical = published?.Sources.SingleOrDefault(source => source.SourceType == selected.SourceType &&
                source.RecordId == selected.SourceRecordId && source.VersionId == selected.SourceVersionId &&
                source.Version == selected.SourceVersion && StringComparer.OrdinalIgnoreCase.Equals(
                    source.LogicalSha256, selected.ContentSha256))?.Content;
            if (published is not null && physical is null)
                return Conflict("The frozen logical source has no complete physical publication proof.");
            if (physical is not null)
            {
                if (selected.SourceContent is not null && selected.SourceContent != physical)
                    return Conflict("The selected physical source differs from its immutable publication proof.");
                if (selected.ByteLength != physical.Bytes || !StringComparer.OrdinalIgnoreCase.Equals(
                        selected.Extension, physical.FileExtension))
                    return Invalid("Use the frozen source's exact byte length and declared file extension.");
                normalized.Add(selected with { SourceContent = physical });
            }
            else
            {
                if (selected.SourceContent is not null)
                    return Conflict("A physical source descriptor requires a frozen publication proof.");
                normalized.Add(selected);
            }
        }
        return Result<AuditPackageContent>.Success(requested with { SelectedContent = normalized });
    }

    static Result<AuditPackageContent> Invalid(string message) =>
        Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result<AuditPackageContent> Conflict(string message) =>
        Result<AuditPackageContent>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
