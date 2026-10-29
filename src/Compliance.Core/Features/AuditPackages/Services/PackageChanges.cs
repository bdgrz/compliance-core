using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Services;

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
