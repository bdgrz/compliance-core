using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Handlers;

/// <summary>Records a management letter against its approved assertion and exact delivered package.</summary>
public sealed class RecordManagementRepresentationLetterHandler(IAggregateExecutor executor,
    IAggregateReader reader, AuditPackageReader packages, TimeProvider clock) : IRequestHandler<RecordManagementRepresentationLetter>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordManagementRepresentationLetter> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording a management representation letter requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ExaminationId == Uuid.Empty || request.LetterId == Uuid.Empty ||
            request.AssertionId == Uuid.Empty || request.ArtifactId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.SourceReference) || request.SourceReference.Trim().Length > 500 ||
            request.LetterDate == default || request.RequiredSignerIds is null ||
            request.RequiredSignerIds.Count is < 1 or > 8 ||
            request.RequiredSignerIds.Any(item => item == Uuid.Empty) ||
            request.RequiredSignerIds.Distinct().Count() != request.RequiredSignerIds.Count)
            return Invalid("A representation letter requires exact source identities, a bounded source reference, date, and unique signers.");

        var assertion = await reader.HydrateAsync(new ManagementAssertion(request.TenantId,
            request.ProgramId, request.AssertionId), ct).ConfigureAwait(false);
        if (assertion.Current is not { } assertionView || assertionView.CurrentVersion is not { } assertionVersion)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The management assertion was not found."));
        var assertionApproval = assertionView.Approvals.FirstOrDefault(approval =>
            approval.AssertionVersion == assertionVersion.Version && approval.Outcome == "approve");
        if (assertionView.ExaminationId != request.ExaminationId ||
            assertionView.Status != "approved" || assertionApproval is null)
            return Conflict("The representation letter requires the approved assertion for this examination.");

        var assertionContent = assertionVersion.Content;
        var package = await ExaminationSignOffSourceValidator.ReadPackageAsync(packages, request.TenantId,
            request.ProgramId, assertionContent.PackageId, ct)
            .ConfigureAwait(false);
        if (!package.IsSuccess)
            return Result.Failure(package.Error!);
        if (package.Value.View is not { } packageView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The assertion's audit package was not found in this program."));
        if (packageView.ExaminationId != request.ExaminationId ||
            packageView.PackageKind != assertionContent.ScopeKind)
            return Conflict("The approved assertion and its audit package do not belong to the requested examination and scope.");

        var packageVersion = packageView.Versions.SingleOrDefault(item =>
            item.PackageVersion == assertionContent.PackageVersion);
        if (packageVersion is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The assertion's exact audit package version was not found."));
        var manifest = packageVersion.Manifest;
        var delivery = packageVersion.Deliveries.FirstOrDefault(item =>
            item.Status == "delivered" && item.PackageVersion == packageVersion.PackageVersion &&
            item.ManifestId == manifest.AuditPackageManifestId &&
            StringComparer.OrdinalIgnoreCase.Equals(item.ManifestSha256, manifest.ManifestSha256));
        var expectedSnapshotKind = packageView.PackageKind switch
        {
            "type_i" => "type_i_baseline",
            "type_ii" => "type_ii_close_snapshot",
            _ => null,
        };
        if (packageVersion.Status != "delivered" || delivery is null || expectedSnapshotKind is null ||
            packageVersion.SourceSnapshotKind != expectedSnapshotKind ||
            manifest.AuditPackageId != packageView.AuditPackageId ||
            manifest.PackageVersion != packageVersion.PackageVersion ||
            manifest.SourceSnapshotId != packageVersion.SourceSnapshotId ||
            !StringComparer.OrdinalIgnoreCase.Equals(manifest.SourceSnapshotSha256,
                packageVersion.SourceSnapshotSha256) ||
            assertionContent.ExaminationId != request.ExaminationId ||
            assertionContent.PackageId != packageView.AuditPackageId ||
            assertionContent.PackageVersion != packageVersion.PackageVersion ||
            assertionContent.PackageManifestId != manifest.AuditPackageManifestId ||
            !StringComparer.OrdinalIgnoreCase.Equals(assertionContent.PackageManifestSha256,
                manifest.ManifestSha256) || assertionContent.SourceSnapshotId != packageVersion.SourceSnapshotId ||
            !StringComparer.OrdinalIgnoreCase.Equals(assertionContent.SourceSnapshotSha256,
                packageVersion.SourceSnapshotSha256))
            return Conflict("The assertion must bind the exact delivered package version, manifest, and frozen source snapshot.");

        var now = clock.GetUtcNow();
        if (now == default || request.LetterDate > DateOnly.FromDateTime(now.UtcDateTime))
            return Invalid("The representation letter date must be present and cannot be in the future.");
        if (request.LetterDate < DateOnly.FromDateTime(delivery.UpdatedAt.UtcDateTime) ||
            request.LetterDate < DateOnly.FromDateTime(assertionApproval.ApprovedAt.UtcDateTime))
            return Conflict("The representation letter date cannot precede the exact package delivery or assertion approval.");

        var assertedSigners = assertionContent.Signers.OrderBy(item => item.MemberId.ToString(),
            StringComparer.Ordinal).ToArray();
        if (!request.RequiredSignerIds.OrderBy(item => item.ToString(), StringComparer.Ordinal)
                .SequenceEqual(assertedSigners.Select(item => item.MemberId)))
            return Conflict("The required representation-letter signers must exactly match the approved assertion.");

        var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
            request.ArtifactId), ct).ConfigureAwait(false);
        if (!artifact.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The original representation letter artifact was not found."));
        if (artifact.State != EvidenceArtifactStates.Available || artifact.ContentSha256 is not { } artifactSha256 ||
            !StringComparer.OrdinalIgnoreCase.Equals(artifact.Content?.Source, "auditor"))
            return Conflict("The original representation letter artifact must be attributed to the auditor and available after inspection.");

        var content = new ManagementRepresentationLetterContent(request.ExaminationId,
            packageView.PackageKind, assertionContent.PackageId,
            packageVersion.PackageVersion, manifest.AuditPackageManifestId, manifest.ManifestSha256,
            request.AssertionId, assertionVersion.Version, assertionVersion.ContentSha256,
            "auditor", request.SourceReference, request.ArtifactId, artifactSha256,
            request.LetterDate, assertedSigners);
        var existingLetter = await reader.HydrateAsync(new ManagementRepresentationLetter(
            request.TenantId, request.ProgramId, request.LetterId), ct).ConfigureAwait(false);
        if (existingLetter.Current is { } existing)
            return SameContent(existing.Content, content) &&
                   existing.RecordedByMemberId == actorMemberId
                ? Result.Success
                : Conflict("The representation letter identity already contains a different immutable record.");

        return await executor.ExecuteAsync(new ManagementRepresentationLetter(request.TenantId,
                request.ProgramId, request.LetterId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Record(content, actorMemberId,
                    actorDisplay, now)), context, ct).ConfigureAwait(false);
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    static bool SameContent(ManagementRepresentationLetterContent left,
        ManagementRepresentationLetterContent right) =>
        left.ExaminationId == right.ExaminationId && left.ScopeKind == right.ScopeKind &&
        left.PackageId == right.PackageId && left.PackageVersion == right.PackageVersion &&
        left.PackageManifestId == right.PackageManifestId &&
        StringComparer.OrdinalIgnoreCase.Equals(left.PackageManifestSha256, right.PackageManifestSha256) &&
        left.AssertionId == right.AssertionId && left.AssertionVersion == right.AssertionVersion &&
        StringComparer.OrdinalIgnoreCase.Equals(left.AssertionContentSha256, right.AssertionContentSha256) &&
        left.SourceKind == right.SourceKind && left.SourceReference == right.SourceReference &&
        left.OriginalArtifactId == right.OriginalArtifactId &&
        StringComparer.OrdinalIgnoreCase.Equals(left.OriginalArtifactSha256, right.OriginalArtifactSha256) &&
        left.LetterDate == right.LetterDate && left.RequiredSigners.SequenceEqual(right.RequiredSigners);
}
