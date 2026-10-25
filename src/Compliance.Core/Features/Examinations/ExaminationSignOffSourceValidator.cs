using Bdgrz.Compliance.Features.Content;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Resolves management's sign-off against the exact immutable sources it claims to review.</summary>
static class ExaminationSignOffSourceValidator
{
    internal static async ValueTask<Result<AuditPackageSnapshot>> ReadPackageAsync(AuditPackageReader packages,
        Uuid tenantId, Uuid programId, Uuid packageId, CancellationToken ct)
    {
        try
        {
            return Result<AuditPackageSnapshot>.Success(await packages.ReadAsync(tenantId, programId,
                packageId, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<AuditPackageSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The audit package's immutable sources cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }

    public static async ValueTask<Result<DateTimeOffset>> ValidateAsync(IAggregateReader reader,
        AuditPackageReader packages, TypeIIPeriodReader periods, ExaminationEngagementReader examinations,
        Uuid tenantId, Uuid programId, ExaminationSignOffContent content, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || content is null ||
            content.ExaminationId == Uuid.Empty || content.ScopeKind is not ("type_i" or "type_ii") ||
            content.KnownExceptions is null || content.UnresolvedMaterialItemIds is null ||
            content.AcknowledgedMaterialItemIds is null || content.Approvers is null ||
            content.Commitments is null)
            return Invalid("A management review requires a complete examination source chain.");

        var package = await ReadPackageAsync(packages, tenantId, programId,
            content.PackageId, ct).ConfigureAwait(false);
        if (!package.IsSuccess)
            return Result<DateTimeOffset>.Failure(package.Error!);
        if (package.Value.View is not { } packageView || packageView.TenantId != tenantId ||
            packageView.ProgramId != programId || packageView.ExaminationId != content.ExaminationId ||
            packageView.PackageKind != content.ScopeKind)
            return Missing("The exact delivered examination package was not found.");
        var packageVersion = packageView.Versions.SingleOrDefault(item =>
            item.PackageVersion == content.PackageVersion);
        var delivery = packageVersion?.Deliveries.FirstOrDefault(item =>
            item.TenantId == tenantId && item.ProgramId == programId &&
            item.AuditPackageId == content.PackageId && item.PackageVersion == content.PackageVersion &&
            item.ManifestId == content.PackageManifestId &&
            HashEquals(item.ManifestSha256, content.PackageManifestSha256) && item.Status == "delivered");
        var expectedSnapshotKind = content.ScopeKind == "type_i"
            ? "type_i_baseline" : "type_ii_close_snapshot";
        if (packageVersion is null || packageVersion.Status != "delivered" || delivery is null ||
            packageVersion.Manifest.AuditPackageManifestId != content.PackageManifestId ||
            !HashEquals(packageVersion.Manifest.ManifestSha256, content.PackageManifestSha256) ||
            packageVersion.SourceSnapshotId != content.SourceSnapshotId ||
            packageVersion.SourceSnapshotKind != expectedSnapshotKind ||
            !HashEquals(packageVersion.SourceSnapshotSha256, content.SourceSnapshotSha256))
            return Conflict("The management review must use the exact delivered package version and manifest.");

        var snapshot = await ReadSnapshotAsync(examinations, periods, tenantId, programId, content,
            packageVersion.SourcePeriodId, ct).ConfigureAwait(false);
        if (!snapshot.IsSuccess)
            return Result<DateTimeOffset>.Failure(snapshot.Error!);
        var sourceReferences = snapshot.Value.References;
        var knownExceptions = sourceReferences.Where(IsException).ToArray();
        var unresolvedExceptionIds = knownExceptions.Select(item => item.RecordId).Distinct().ToArray();
        if (!ExactReferenceSet(content.KnownExceptions, knownExceptions) ||
            content.ScopeKind == "type_i" && !ExactIdSet(content.UnresolvedMaterialItemIds,
                unresolvedExceptionIds) ||
            content.Commitments.Any(commitment => commitment is null ||
                !sourceReferences.Any(source => source.SourceType == commitment.SourceType &&
                    source.RecordId == commitment.SourceRecordId &&
                    source.Version == commitment.SourceRecordVersion &&
                    HashEquals(source.ContentSha256, commitment.SourceContentSha256))))
            return Conflict("Known exceptions and management commitments must belong to the exact frozen source snapshot.");
        foreach (var commitment in content.Commitments)
        {
            var owner = await reader.HydrateAsync(Member.ForVerification(tenantId,
                commitment.OwnerMemberId), ct).ConfigureAwait(false);
            if (!owner.IsRegistered || owner.IsSuspended || owner.IsDeprovisioned ||
                owner.Affiliation != "client_personnel" || owner.UserId == Uuid.Empty)
                return Conflict("Every management follow-up commitment must have a current client owner.");
        }

        var assertion = await reader.HydrateAsync(new ManagementAssertion(tenantId, programId,
            content.AssertionId), ct).ConfigureAwait(false);
        var assertionView = assertion.Current;
        var assertionVersion = assertionView?.Versions.SingleOrDefault(item =>
            item.Version == content.AssertionVersion);
        var approved = assertionView?.Approvals.SingleOrDefault(item =>
            item.AssertionVersion == content.AssertionVersion && item.Outcome == "approve");
        if (assertionView is null || assertionView.TenantId != tenantId ||
            assertionView.ProgramId != programId || assertionView.ExaminationId != content.ExaminationId ||
            assertionVersion is null || approved is null ||
            !HashEquals(assertionVersion.ContentSha256, content.AssertionContentSha256) ||
            assertionVersion.Content.ScopeKind != content.ScopeKind ||
            assertionVersion.Content.SourceSnapshotId != content.SourceSnapshotId ||
            !HashEquals(assertionVersion.Content.SourceSnapshotSha256, content.SourceSnapshotSha256) ||
            !sourceReferences.Any(source => source.SourceType == "system_description" &&
                source.VersionId == assertionVersion.Content.SystemDescriptionVersionId &&
                HashEquals(source.ContentSha256, assertionVersion.Content.SystemDescriptionSha256)) ||
            !ExactReferences(assertionVersion.Content.SupportingInputs, sourceReferences) ||
            assertionVersion.Content.PackageId != content.PackageId ||
            assertionVersion.Content.PackageVersion != content.PackageVersion ||
            assertionVersion.Content.PackageManifestId != content.PackageManifestId ||
            !HashEquals(assertionVersion.Content.PackageManifestSha256, content.PackageManifestSha256) ||
            assertionVersion.Content.SupportingInputs.Any(input =>
                !IncludedInPackage(packageVersion, input)) ||
            content.KnownExceptions.Any(exception => !IncludedInPackage(packageVersion, exception)))
            return Conflict("The management review must bind the approved assertion for the exact package and snapshot.");

        var letter = await reader.HydrateAsync(new ManagementRepresentationLetter(tenantId,
            programId, content.RepresentationLetterId), ct).ConfigureAwait(false);
        var letterView = letter.Current;
        if (letterView is not { Status: "signed" } || letterView.TenantId != tenantId ||
            letterView.ProgramId != programId || letterView.Content.ExaminationId != content.ExaminationId ||
            letterView.Content.ScopeKind != content.ScopeKind ||
            letterView.Content.PackageId != content.PackageId ||
            letterView.Content.PackageVersion != content.PackageVersion ||
            letterView.Content.PackageManifestId != content.PackageManifestId ||
            !HashEquals(letterView.Content.PackageManifestSha256, content.PackageManifestSha256) ||
            letterView.Content.AssertionId != content.AssertionId ||
            letterView.Content.AssertionVersion != content.AssertionVersion ||
            !HashEquals(letterView.Content.AssertionContentSha256, content.AssertionContentSha256) ||
            letterView.Signatures.Count != letterView.Content.RequiredSigners.Count ||
            !HashEquals(Digest(letterView.Content), content.RepresentationLetterContentSha256))
            return Conflict("The management review must bind the fully signed external representation letter for its approved assertion.");
        var originalLetterArtifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId,
            letterView.Content.OriginalArtifactId), ct).ConfigureAwait(false);
        if (!originalLetterArtifact.IsCreated ||
            originalLetterArtifact.State != EvidenceArtifactStates.Available ||
            !HashEquals(originalLetterArtifact.ContentSha256,
                letterView.Content.OriginalArtifactSha256))
            return Conflict("The auditor's original representation-letter artifact must remain available and unchanged.");
        foreach (var signature in letterView.Signatures)
        {
            var signedArtifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId,
                signature.SignedArtifactId), ct).ConfigureAwait(false);
            if (!signedArtifact.IsCreated || signedArtifact.State != EvidenceArtifactStates.Available ||
                !HashEquals(signedArtifact.ContentSha256, signature.SignedArtifactSha256))
                return Conflict("Every management signature must retain its exact available signed artifact.");
        }

        var outcome = await reader.HydrateAsync(new AuditOutcome(tenantId, programId,
            content.AuditOutcomeId), ct).ConfigureAwait(false);
        if (outcome.Current is not { Status: "recorded" } outcomeView ||
            outcomeView.TenantId != tenantId || outcomeView.ProgramId != programId ||
            outcomeView.Content.ExaminationId != content.ExaminationId ||
            outcomeView.Content.ScopeKind != content.ScopeKind ||
            outcomeView.Content.SourceKind != "auditor" ||
            outcomeView.Content.SourceSnapshotId != content.SourceSnapshotId ||
            !HashEquals(outcomeView.Content.SourceSnapshotSha256, content.SourceSnapshotSha256) ||
            outcomeView.Content.PackageId != content.PackageId ||
            outcomeView.Content.PackageVersion != content.PackageVersion ||
            outcomeView.Content.PackageManifestId != content.PackageManifestId ||
            !HashEquals(outcomeView.Content.PackageManifestSha256, content.PackageManifestSha256) ||
            !HashEquals(outcomeView.ContentSha256, content.AuditOutcomeContentSha256))
            return Conflict("The management review must bind the auditor's recorded outcome for the exact package and snapshot.");

        var sourceTime = new[] { snapshot.Value.RecordedAt, delivery.UpdatedAt,
            approved.ApprovedAt, letterView.Signatures.Max(item => item.SignedAt), outcomeView.RecordedAt }
            .Max();
        return Result<DateTimeOffset>.Success(sourceTime);
    }

    static async ValueTask<Result<SnapshotSources>> ReadSnapshotAsync(ExaminationEngagementReader examinations, TypeIIPeriodReader periods,
        Uuid tenantId, Uuid programId, ExaminationSignOffContent content, Uuid sourcePeriodId,
        CancellationToken ct)
    {
        if (content.ScopeKind == "type_i")
        {
            var baseline = await examinations.ReadBaselineAsync(tenantId, programId, content.ExaminationId,
                content.SourceSnapshotId, ct).ConfigureAwait(false);
            if (baseline is null || baseline.TenantId != tenantId || baseline.ProgramId != programId ||
                baseline.ExaminationEngagementId != content.ExaminationId ||
                baseline.Status is not ("frozen" or "amended") ||
                !HashEquals(baseline.ContentSha256, content.SourceSnapshotSha256))
                return Failure<SnapshotSources>(RequestErrorKind.NotFound,
                    "The exact frozen Type I baseline was not found.");
            return Result<SnapshotSources>.Success(new(baseline.Sources.Select(item =>
                new ExaminationContentReference(item.Kind, item.RecordId, item.VersionId,
                    item.Revision, item.ContentSha256)).ToArray(), baseline.UpdatedAt));
        }

        if (sourcePeriodId == Uuid.Empty)
            return Failure<SnapshotSources>(RequestErrorKind.Conflict,
                "A Type II package must retain its source period identity.");
        var periodRead = await periods.TryReadAsync(tenantId, programId, sourcePeriodId, ct).ConfigureAwait(false);
        if (!periodRead.IsSuccess)
            return Result<SnapshotSources>.Failure(periodRead.Error!);
        var period = periodRead.Value;
        var closeSnapshot = period.View?.CloseSnapshots.SingleOrDefault(item =>
            item.CloseSnapshotId == content.SourceSnapshotId);
        if (period.View is not { } periodView || periodView.TenantId != tenantId ||
            periodView.ProgramId != programId || periodView.ExaminationId != content.ExaminationId ||
            closeSnapshot is null || !HashEquals(closeSnapshot.ContentSha256,
                content.SourceSnapshotSha256) ||
            !closeSnapshot.AcknowledgedMaterialItemIds.OrderBy(IdKey, StringComparer.Ordinal)
                .SequenceEqual(content.UnresolvedMaterialItemIds.OrderBy(IdKey, StringComparer.Ordinal)))
            return Failure<SnapshotSources>(RequestErrorKind.Conflict,
                "The management review must bind the exact frozen Type II close snapshot and acknowledged items.");
        return Result<SnapshotSources>.Success(new(closeSnapshot.SourceReferences,
            closeSnapshot.RecordedAt));
    }

    static bool ExactReferences(IReadOnlyList<ExaminationContentReference> references,
        IReadOnlyList<ExaminationContentReference> sources) =>
        references is not null && references.All(reference => reference is not null &&
            sources.Any(source =>
            source.SourceType == reference.SourceType && source.RecordId == reference.RecordId &&
            source.VersionId == reference.VersionId && source.Version == reference.Version &&
            HashEquals(source.ContentSha256, reference.ContentSha256)));

    static bool ExactReferenceSet(IReadOnlyList<ExaminationContentReference> actual,
        ExaminationContentReference[] expected)
    {
        if (actual is null || expected is null || actual.Count != expected.Length ||
            actual.Any(static item => item is null) ||
            actual.Select(static item => (item.SourceType, item.RecordId, item.VersionId))
                .Distinct().Count() != actual.Count)
            return false;
        return actual.All(item => item is not null && expected.Any(source =>
            source.SourceType == item.SourceType &&
            source.RecordId == item.RecordId && source.VersionId == item.VersionId &&
            source.Version == item.Version && HashEquals(source.ContentSha256, item.ContentSha256)));
    }

    static bool ExactIdSet(IReadOnlyList<Uuid> actual, IReadOnlyList<Uuid> expected) =>
        actual is not null && actual.Distinct().Count() == actual.Count &&
        actual.OrderBy(IdKey, StringComparer.Ordinal)
            .SequenceEqual(expected.Distinct().OrderBy(IdKey, StringComparer.Ordinal));

    static bool IsException(ExaminationContentReference source) =>
        source.SourceType is "finding" or "audit_observation";

    static bool IncludedInPackage(AuditPackageVersionView package,
        ExaminationContentReference reference) => package.SelectedContent.Any(item =>
        item.SourceType == reference.SourceType && item.SourceRecordId == reference.RecordId &&
        item.SourceVersionId == reference.VersionId && item.SourceVersion == reference.Version &&
        HashEquals(item.ContentSha256, reference.ContentSha256));

    static string Digest<T>(T value) => Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    static string IdKey(Uuid id) => id.ToString();

    static bool HashEquals(string? left, string? right) => left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    static Result<DateTimeOffset> Invalid(string message) =>
        Result<DateTimeOffset>.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result<DateTimeOffset> Missing(string message) =>
        Result<DateTimeOffset>.Failure(new RequestError(RequestErrorKind.NotFound, message));

    static Result<DateTimeOffset> Conflict(string message) =>
        Result<DateTimeOffset>.Failure(new RequestError(RequestErrorKind.Conflict, message));

    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));

    sealed record SnapshotSources(IReadOnlyList<ExaminationContentReference> References,
        DateTimeOffset RecordedAt);
}
