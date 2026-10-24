using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the external auditor's result against its exact delivered package and report.</summary>
public sealed class RecordExaminationOutcomeHandler(IAggregateExecutor executor,
    IAggregateReader reader, AuditPackageReader packages, TimeProvider clock) : IRequestHandler<RecordExaminationOutcome>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordExaminationOutcome> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording an examination outcome requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ExaminationId == Uuid.Empty || request.OutcomeId == Uuid.Empty ||
            request.PackageId == Uuid.Empty || request.PackageVersion < 1 ||
            request.ReportArtifactId == Uuid.Empty)
            return Invalid("An examination outcome requires exact examination, package, version, and outcome identities.");

        var package = await ExaminationSignOffSourceValidator.ReadPackageAsync(packages, request.TenantId,
            request.ProgramId, request.PackageId, ct)
            .ConfigureAwait(false);
        if (!package.IsSuccess)
            return Result.Failure(package.Error!);
        if (package.Value.View is not { } packageView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The audit package was not found in this program."));
        if (packageView.ExaminationId != request.ExaminationId)
            return Conflict("The audit package belongs to a different examination.");

        var version = packageView.Versions.SingleOrDefault(item =>
            item.PackageVersion == request.PackageVersion);
        if (version is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The audit package version was not found."));
        var manifest = version.Manifest;
        var delivery = version.Deliveries.FirstOrDefault(delivery =>
                delivery.Status == "delivered" && delivery.PackageVersion == version.PackageVersion &&
                delivery.ManifestId == manifest.AuditPackageManifestId &&
                StringComparer.OrdinalIgnoreCase.Equals(delivery.ManifestSha256, manifest.ManifestSha256));
        if (version.Status != "delivered" || delivery is null)
            return Conflict("An auditor outcome must bind to a package version that was delivered.");
        if (request.IssuedOn == default ||
            request.IssuedOn < DateOnly.FromDateTime(delivery.UpdatedAt.UtcDateTime))
            return Conflict("The auditor report date cannot precede delivery of its exact package version.");
        var expectedSourceKind = packageView.PackageKind switch
        {
            "type_i" => "type_i_baseline",
            "type_ii" => "type_ii_close_snapshot",
            _ => null,
        };
        if (manifest.AuditPackageId != packageView.AuditPackageId ||
            manifest.PackageVersion != version.PackageVersion ||
            manifest.SourceSnapshotId != version.SourceSnapshotId ||
            !StringComparer.OrdinalIgnoreCase.Equals(manifest.SourceSnapshotSha256,
                version.SourceSnapshotSha256) ||
            expectedSourceKind is null || version.SourceSnapshotKind != expectedSourceKind)
            return Conflict("The delivered package version has inconsistent frozen source or manifest identity.");

        string? reportArtifactSha256 = null;
        if (request.ReportArtifactId is { } reportArtifactId)
        {
            var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
                reportArtifactId), ct).ConfigureAwait(false);
            if (!artifact.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The issued auditor report artifact was not found."));
            if (artifact.State != EvidenceArtifactStates.Available ||
                artifact.ContentSha256 is not { } availableReportHash)
                return Conflict("The auditor report artifact must be available after inspection.");
            reportArtifactSha256 = availableReportHash;
        }

        var recordedAt = clock.GetUtcNow();
        if (recordedAt == default || request.IssuedOn > DateOnly.FromDateTime(recordedAt.UtcDateTime))
            return Invalid("The auditor report date must be present and cannot be in the future.");

        var content = new AuditOutcomeContent(request.ExaminationId, packageView.PackageKind,
            version.SourceSnapshotId, version.SourceSnapshotSha256, packageView.AuditPackageId,
            version.PackageVersion, manifest.AuditPackageManifestId, manifest.ManifestSha256,
            "auditor", request.OutcomeCode, request.OutcomeSummary, request.IssuedReportReference,
            request.ReportArtifactId, reportArtifactSha256, request.IssuedOn);
        return await executor.ExecuteAsync(new AuditOutcome(request.TenantId, request.ProgramId,
                request.OutcomeId), outcome => CommandFailureRequestAdapter.ToOutcome(
                outcome.Record(content, actorMemberId, actorDisplay, recordedAt)), context, ct)
            .ConfigureAwait(false);
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
