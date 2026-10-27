using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Event-specific deltas and pure rule error adaptation; no full parent snapshots are persisted.</summary>
static class PackageChanges
{
    public static Result<PreparedPackageDelta> Failure(CommandFailure failure) =>
        Result<PreparedPackageDelta>.Failure(failure.ToRequestError());
    public static Result<PreparedPackageDelta> Invalid(string message) =>
        Result<PreparedPackageDelta>.Failure(new RequestError(RequestErrorKind.Validation, message));
    public static Result<PreparedPackageDelta> Conflict(string message) =>
        Result<PreparedPackageDelta>.Failure(new RequestError(RequestErrorKind.Conflict, message));
    public static Uuid Member(OperationProvenance operation) =>
        Uuid.Parse(operation.Actor.Id, CultureInfo.InvariantCulture);
    public static AuditPackageContent Content(AuditPackageView view, AuditPackageVersionView version) =>
        new(view.ExaminationId, view.PackageKind, version.SourceSnapshotKind, version.SourceSnapshotId,
            version.SourceSnapshotSha256, view.Name, version.SelectedContent, version.SourcePeriodId);
    public static AuditPackageManifestVersionContent Version(AuditPackageView view,
        OperationProvenance operation)
    {
        var version = view.Versions[^1];
        return new AuditPackageManifestVersionContent(view.ProgramId, Content(view, version), version.Manifest,
            version.AmendsVersion, version.AmendmentReason, operation.Actor, operation.AcceptedAt);
    }
}
