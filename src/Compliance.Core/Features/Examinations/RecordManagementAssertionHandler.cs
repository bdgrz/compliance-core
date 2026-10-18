using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records management wording against its exact validated examination sources.</summary>
public sealed class RecordManagementAssertionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<RecordManagementAssertion>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordManagementAssertion> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording a management assertion requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ExaminationId == Uuid.Empty ||
            request.AssertionId == Uuid.Empty || request.Content is not { } content ||
            content.ExaminationId == Uuid.Empty || content.PackageId == Uuid.Empty ||
            content.PackageVersion <= 0 || content.SystemDescriptionVersionId == Uuid.Empty)
            return Invalid("A management assertion requires exact examination, package, and system-description identities.");
        if (request.ExaminationId != content.ExaminationId)
            return Conflict("The examination route identity must match the assertion's frozen source content.");

        var package = await reader.HydrateAsync(new AuditPackage(request.TenantId,
            request.ProgramId, content.PackageId), ct).ConfigureAwait(false);
        if (package.Current is not { } packageView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The assertion's audit package was not found in this program."));
        if (packageView.ExaminationId != content.ExaminationId ||
            packageView.PackageKind != content.ScopeKind)
            return Conflict("The assertion package must belong to the exact examination and assertion scope.");
        var packageVersion = packageView.Versions.SingleOrDefault(item =>
            item.PackageVersion == content.PackageVersion);
        if (packageVersion is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The exact assertion package version was not found."));
        var manifest = packageVersion.Manifest;
        var validation = packageVersion.Validations.Count == 0
            ? null : packageVersion.Validations[^1];
        if (packageVersion.Status is not ("validated" or "validated_with_overrides" or
                "disclosure_approved" or "delivered") ||
            validation is not { Status: "passed" or "passed_with_overrides" } ||
            validation.PackageVersion != packageVersion.PackageVersion ||
            validation.ManifestId != manifest.AuditPackageManifestId ||
            !HashEquals(validation.ManifestSha256, manifest.ManifestSha256) ||
            manifest.TenantId != request.TenantId || manifest.ProgramId != request.ProgramId ||
            manifest.AuditPackageId != packageView.AuditPackageId ||
            manifest.PackageVersion != packageVersion.PackageVersion ||
            manifest.SourceSnapshotId != packageVersion.SourceSnapshotId ||
            !HashEquals(manifest.SourceSnapshotSha256, packageVersion.SourceSnapshotSha256) ||
            content.PackageId != packageView.AuditPackageId ||
            content.PackageManifestId != manifest.AuditPackageManifestId ||
            !HashEquals(content.PackageManifestSha256, manifest.ManifestSha256) ||
            content.SourceSnapshotId != packageVersion.SourceSnapshotId ||
            !HashEquals(content.SourceSnapshotSha256, packageVersion.SourceSnapshotSha256))
            return Conflict("Management wording must bind a validated exact package manifest and frozen source snapshot.");

        var now = clock.GetUtcNow();
        if (now == default || now < packageView.CreatedAt || now < packageVersion.CreatedAt ||
            now < validation.UpdatedAt)
            return Conflict("The assertion cannot precede package creation and successful validation.");

        var sourceResult = await ResolveSnapshotAsync(request.TenantId, request.ProgramId,
            content.ExaminationId, packageVersion, ct).ConfigureAwait(false);
        if (sourceResult.Error is not null)
            return Result.Failure(sourceResult.Error);
        var snapshot = sourceResult.Sources!;
        var descriptionSelection = FindSystemDescriptionSelection(packageVersion.SelectedContent,
            content.SystemDescriptionVersionId);
        if (!StringComparer.Ordinal.Equals(content.ScopeKind, packageView.PackageKind) ||
            descriptionSelection is null ||
            !MatchesSystemDescriptionSource(snapshot, descriptionSelection.SourceRecordId,
                content.SystemDescriptionVersionId, content.SystemDescriptionSha256) ||
            descriptionSelection.SourceVersion <= 0 ||
            !HashEquals(descriptionSelection.ContentSha256, content.SystemDescriptionSha256) ||
            content.SupportingInputs is null || content.SupportingInputs.Count == 0 ||
            content.SupportingInputs.Any(input => !MatchesSnapshot(snapshot, input) ||
                !MatchesPackageSelection(packageVersion.SelectedContent, input)))
            return Conflict("Every assertion input and system-description version must match the exact validated package and frozen snapshot.");

        var descriptionId = descriptionSelection.SourceRecordId;
        var description = await reader.HydrateAsync(new SystemDescriptionAggregate(request.TenantId,
            request.ProgramId, descriptionId), ct).ConfigureAwait(false);
        var descriptionView = description.Read();
        var descriptionVersion = description.FindVersion(content.SystemDescriptionVersionId);
        var purpose = content.ScopeKind == "type_i" ? "type_i_baseline" : "type_ii_close";
        var approval = descriptionView.Approvals.FirstOrDefault(item =>
            item.VersionId == content.SystemDescriptionVersionId);
        var binding = descriptionView.Bindings.FirstOrDefault(item =>
            item.VersionId == content.SystemDescriptionVersionId && item.Purpose == purpose);
        if (descriptionVersion is not { Status: "approved" } ||
            descriptionVersion.TenantId != request.TenantId ||
            descriptionVersion.ProgramId != request.ProgramId ||
            descriptionVersion.SystemDescriptionId != descriptionId ||
            descriptionVersion.Scope.ExaminationId != content.ExaminationId ||
            descriptionVersion.Scope.Stage != content.ScopeKind ||
            content.ScopeKind == "type_i" &&
                descriptionVersion.Scope.TypeIBaselineId != content.SourceSnapshotId ||
            content.ScopeKind == "type_ii" &&
                descriptionVersion.Scope.CloseSnapshotId != content.SourceSnapshotId ||
            descriptionVersion.ContentSha256 is not { } descriptionHash ||
            !HashEquals(descriptionHash, content.SystemDescriptionSha256) ||
            approval is null || approval.Version != descriptionVersion.Version ||
            binding is null || binding.Version != descriptionVersion.Version ||
            binding.ExaminationId != content.ExaminationId ||
            binding.SnapshotId != content.SourceSnapshotId || now < descriptionVersion.UpdatedAt ||
            now < approval.ApprovedAt || now < binding.BoundAt)
            return Conflict("The assertion must use an approved system description bound to the exact examination snapshot.");

        if (content.WordingReferenceId is not { } wordingReferenceId || wordingReferenceId == Uuid.Empty)
            return Invalid("Management assertion wording requires a stable source reference.");
        var wordingSource = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
            wordingReferenceId), ct).ConfigureAwait(false);
        if (!wordingSource.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The assertion wording source was not found."));
        if (wordingSource.State != EvidenceArtifactStates.Available ||
            wordingSource.ContentSha256 is null || wordingSource.Content is not { } wordingContent ||
            !StringComparer.OrdinalIgnoreCase.Equals(wordingContent.Source, content.WordingSource))
            return Conflict("The assertion wording reference must be an inspected artifact with matching source attribution.");

        return await executor.ExecuteAsync(new ManagementAssertion(request.TenantId,
                request.ProgramId, request.AssertionId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Create(content,
                    actorMemberId, actorDisplay, now)), context, ct).ConfigureAwait(false);
    }

    async ValueTask<(IReadOnlyList<ExaminationContentReference>? Sources, RequestError? Error)>
        ResolveSnapshotAsync(Uuid tenantId, Uuid programId, Uuid examinationId,
            AuditPackageVersionView packageVersion, CancellationToken ct)
    {
        if (packageVersion.SourceSnapshotKind == "type_i_baseline" &&
            packageVersion.SourcePeriodId == Uuid.Empty)
        {
            var engagement = await reader.HydrateAsync(new ExaminationEngagement(tenantId,
                programId, examinationId), ct).ConfigureAwait(false);
            if (engagement.Current is null)
                return (null, new RequestError(RequestErrorKind.NotFound,
                    "The Type I examination engagement was not found."));
            var baseline = engagement.BaselineHistory.SingleOrDefault(item =>
                item.TypeIBaselineId == packageVersion.SourceSnapshotId);
            if (baseline is not { Status: "frozen" } || baseline.ExaminationEngagementId != examinationId ||
                !HashEquals(baseline.ContentSha256, packageVersion.SourceSnapshotSha256))
                return (null, new RequestError(RequestErrorKind.Conflict,
                    "The package must use an exact frozen Type I baseline for this examination."));
            return (baseline.Sources.Select(item => new ExaminationContentReference(item.Kind,
                item.RecordId, item.VersionId, item.Revision, item.ContentSha256)).ToArray(), null);
        }

        if (packageVersion.SourceSnapshotKind == "type_ii_close_snapshot" &&
            packageVersion.SourcePeriodId != Uuid.Empty)
        {
            var period = await reader.HydrateAsync(new TypeIIPeriod(tenantId, programId,
                packageVersion.SourcePeriodId), ct).ConfigureAwait(false);
            if (period.Current is not { Status: "closed" } closed ||
                closed.ExaminationId != examinationId)
                return (null, new RequestError(RequestErrorKind.Conflict,
                    "The package must use a closed Type II period for this examination."));
            var snapshot = closed.CloseSnapshots.SingleOrDefault(item =>
                item.CloseSnapshotId == packageVersion.SourceSnapshotId);
            if (snapshot is null || !HashEquals(snapshot.ContentSha256,
                    packageVersion.SourceSnapshotSha256))
                return (null, new RequestError(RequestErrorKind.Conflict,
                    "The package must use an exact frozen Type II close snapshot."));
            return (snapshot.SourceReferences, null);
        }

        return (null, new RequestError(RequestErrorKind.Conflict,
            "The package source must be a supported frozen Type I or Type II examination snapshot."));
    }

    static bool MatchesSystemDescriptionSource(IReadOnlyList<ExaminationContentReference> sources,
        Uuid descriptionId, Uuid versionId, string? sha256) => sources.Any(item =>
        item.SourceType == "system_description" && item.RecordId == descriptionId &&
        item.VersionId == versionId && item.Version > 0 && HashEquals(item.ContentSha256, sha256));

    static AuditPackageSelectionEntry? FindSystemDescriptionSelection(
        IReadOnlyList<AuditPackageSelectionEntry> selections, Uuid versionId)
    {
        for (var index = 0; index < selections.Count; index++)
        {
            var item = selections[index];
            if (item.SourceType == "system_description" && item.SourceVersionId == versionId)
                return item;
        }
        return null;
    }

    static bool MatchesSnapshot(IReadOnlyList<ExaminationContentReference> sources,
        ExaminationContentReference input) => sources.Any(item =>
        item.SourceType == input.SourceType && item.RecordId == input.RecordId &&
        item.VersionId == input.VersionId && item.Version == input.Version &&
        HashEquals(item.ContentSha256, input.ContentSha256));

    static bool MatchesPackageSelection(IReadOnlyList<AuditPackageSelectionEntry> selections,
        ExaminationContentReference input) => selections.Any(item =>
        item.SourceType == input.SourceType && item.SourceRecordId == input.RecordId &&
        item.SourceVersionId == input.VersionId && item.SourceVersion == input.Version &&
        HashEquals(item.ContentSha256, input.ContentSha256));

    static bool HashEquals(string? left, string? right) =>
        left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
