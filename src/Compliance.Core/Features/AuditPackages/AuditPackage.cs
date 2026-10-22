using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>
/// One package stream owns its frozen source selection, deterministic manifest,
/// build attempts, validation decisions, exact-recipient disclosures, deliveries,
/// and immutable amendment lineage. Source authorization and freshness are checked
/// by handlers before they provide package content or validation results.
/// </summary>
public sealed class AuditPackage : Aggregate
{
    const int MaximumSelectedContent = 256;
    const int MaximumBuildAttempts = 64;
    const int MaximumValidations = 64;
    const int MaximumValidationFindings = 256;
    const int MaximumDisclosures = 64;
    const int MaximumDeliveries = 128;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    const int MaximumNameLength = 200;
    const int MaximumTextLength = 2000;
    const int MaximumTokenLength = 100;
    static readonly Uuid ManifestNamespace = Uuid.Parse(
        "e7b7f48b-90ad-57a1-a2b7-14caf24d9714", CultureInfo.InvariantCulture);
    static readonly Uuid BuildNamespace = Uuid.Parse(
        "698c0bb6-2dfc-58b3-b2fd-2f3dc04369f8", CultureInfo.InvariantCulture);
    static readonly Uuid ValidationNamespace = Uuid.Parse(
        "5fb9c6a1-1607-51d5-aef7-0b51458c2698", CultureInfo.InvariantCulture);
    static readonly Uuid DisclosureNamespace = Uuid.Parse(
        "71eaf915-dcd6-5d96-8b24-86ca2b6268d1", CultureInfo.InvariantCulture);
    static readonly Uuid DeliveryNamespace = Uuid.Parse(
        "53cb20d6-01ef-5fd5-99b3-04087e39bf77", CultureInfo.InvariantCulture);
    static readonly HashSet<string> PackageKinds = new(StringComparer.Ordinal)
    {
        "type_i", "type_ii"
    };
    static readonly HashSet<string> AuthorshipKinds = new(StringComparer.Ordinal)
    {
        "management", "auditor", "product_calculated"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    AuditPackageView? _current;

    public long Revision => _current?.Revision ?? 0;

    public AuditPackageView? Current => _current;

    public AuditPackage(Uuid tenantId, Uuid programId, Uuid packageId)
        : base(packageId, new EventStreamAddress(tenantId.ToString(), "audit-packages",
            packageId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<AuditPackageCreated>(Apply);
        On<AuditPackageBuildRequested>(Apply);
        On<AuditPackageBuildProgressRecorded>(Apply);
        On<AuditPackageBuildFailed>(Apply);
        On<AuditPackageBuilt>(Apply);
        On<AuditPackageValidated>(Apply);
        On<AuditPackageValidationOverrideApproved>(Apply);
        On<AuditPackageDisclosureApproved>(Apply);
        On<AuditPackageDelivered>(Apply);
        On<AuditPackageAmended>(Apply);
    }

    public CommandFailure? Create(AuditPackageContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset createdAt)
    {
        if (_current is { } existing)
            return SameInitialContent(existing, content, actorMemberId, actorDisplay, createdAt)
                ? null
                : CommandFailure.StateConflict(
                    "The audit package already exists with different scope, selection, or attribution.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty)
            return Invalid("An audit package requires tenant, program, and package identities.");
        if (ValidateHumanActor(actorMemberId, actorDisplay, createdAt) is { } actorFailure)
            return actorFailure;
        if (content?.PackageKind == "type_ii" && content.SourcePeriodId == Uuid.Empty)
            return Invalid("A Type II package must retain the identity of its owning period.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("An audit package requires a frozen examination snapshot and explicit valid content selection.");

        var manifest = CreateManifest(normalized, 1, createdAt);
        var version = NewVersion(normalized, manifest, 1, null, null, actorMemberId,
            actorDisplay.Trim(), createdAt);
        var state = new AuditPackageView(_tenantId, _programId, Id,
            normalized.ExaminationId, normalized.PackageKind, normalized.Name, "draft", 1,
            1, createdAt, actorMemberId, actorDisplay.Trim(), createdAt, [version]);
        return Emit(state, actorMemberId, createdAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageCreated(tenant, package,
            revision, actor, snapshot, occurredAt), creating: true);
    }

    public CommandFailure? RequestBuild(long expectedRevision, string buildKey,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset requestedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        buildKey = Clean(buildKey);
        if (!Token(buildKey, MaximumTokenLength) ||
            ValidateHumanActor(actorMemberId, actorDisplay, requestedAt) is { } actorFailure)
            return Invalid("A package build requires an idempotency key and attributed member.");
        var version = CurrentVersion(current);
        var attemptId = Uuid.CreateVersion5(BuildNamespace, $"{Id}\n{version.PackageVersion}\n{buildKey}");
        if (version.BuildAttempts.FirstOrDefault(attempt => attempt.BuildAttemptId == attemptId) is { } prior)
            return prior.BuildKey == buildKey && prior.RequestedByMemberId == actorMemberId
                ? null
                : CommandFailure.StateConflict("A build key cannot be reused with changed intent or attribution.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (version.Deliveries.Count > 0 || version.BuildAttempts.Count >= MaximumBuildAttempts)
            return CommandFailure.StateConflict(
                "A delivered version is immutable, and build attempts are bounded; amend it to create another version.");

        var attempt = new AuditPackageBuildAttemptView(attemptId, buildKey, "requested", 0,
            version.Manifest.Entries.Count, requestedAt, requestedAt, actorMemberId,
            actorDisplay.Trim(), null, null, null, null, null, null);
        var nextVersion = version with
        {
            Status = "building",
            BuildAttempts = Append(version.BuildAttempts, attempt),
        };
        var next = ReplaceCurrentVersion(current, nextVersion, "building", requestedAt);
        return Emit(next, actorMemberId, requestedAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageBuildRequested(tenant,
            package, revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? RecordBuildProgress(long expectedRevision, Uuid buildAttemptId,
        int completedEntryCount, int totalEntryCount, Uuid? actorMemberId,
        string actorDisplay, DateTimeOffset updatedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateActor(actorMemberId, actorDisplay, updatedAt) is { } actorFailure)
            return actorFailure;
        var version = CurrentVersion(current);
        var attemptIndex = IndexOfAttempt(version, buildAttemptId);
        if (attemptIndex < 0)
            return CommandFailure.MissingRecord("The package build attempt was not found.");
        var attempt = version.BuildAttempts[attemptIndex];
        if (attempt.Status == "running" && completedEntryCount == attempt.CompletedEntryCount &&
            totalEntryCount == attempt.TotalEntryCount)
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (attempt.Status is not ("requested" or "running") ||
            totalEntryCount != attempt.TotalEntryCount || completedEntryCount < attempt.CompletedEntryCount ||
            completedEntryCount < 0 || completedEntryCount >= totalEntryCount)
            return CommandFailure.StateConflict(
                "Build progress must be monotonic, bounded, and recorded before completion.");
        var nextAttempt = attempt with
        {
            Status = "running",
            CompletedEntryCount = completedEntryCount,
            UpdatedAt = updatedAt,
        };
        var nextVersion = version with
        {
            Status = "building",
            BuildAttempts = Replace(version.BuildAttempts, attemptIndex, nextAttempt),
        };
        var next = ReplaceCurrentVersion(current, nextVersion, "building", updatedAt);
        return Emit(next, actorMemberId, updatedAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageBuildProgressRecorded(tenant,
            package, revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? RecordBuildResult(long expectedRevision, Uuid buildAttemptId,
        AuditPackageBuildOutput? output, Uuid? actorMemberId, string actorDisplay,
        DateTimeOffset completedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateActor(actorMemberId, actorDisplay, completedAt) is { } actorFailure)
            return actorFailure;
        var version = CurrentVersion(current);
        var attemptIndex = IndexOfAttempt(version, buildAttemptId);
        if (attemptIndex < 0)
            return CommandFailure.MissingRecord("The package build attempt was not found.");
        var attempt = version.BuildAttempts[attemptIndex];
        if (attempt.Status is "built" or "failed")
            return BuildResultAlreadyRecorded(attempt, output)
                ? null
                : CommandFailure.StateConflict("A package build result is immutable after it is recorded.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (attempt.Status is not ("requested" or "running"))
            return CommandFailure.StateConflict("The package build was not requested.");

        var failureCode = ValidateBuildOutput(version, attempt, output);
        var succeeds = failureCode is null;
        var nextAttempt = attempt with
        {
            Status = succeeds ? "built" : "failed",
            CompletedEntryCount = succeeds ? attempt.TotalEntryCount : attempt.CompletedEntryCount,
            UpdatedAt = completedAt,
            CompletedAt = completedAt,
            FailureCode = failureCode,
            ResultFingerprint = BuildResultFingerprint(output),
            ArchiveArtifactId = succeeds ? output?.ArchiveArtifactId : null,
            ArchiveSha256 = succeeds ? NormalizeHash(output?.ArchiveSha256) : null,
            ArchiveBytes = succeeds ? output?.ArchiveBytes : null,
        };
        var nextVersion = version with
        {
            Status = succeeds ? "built" : "build_failed",
            BuildAttempts = Replace(version.BuildAttempts, attemptIndex, nextAttempt),
        };
        var next = ReplaceCurrentVersion(current, nextVersion,
            succeeds ? "built" : "build_failed", completedAt);
        return succeeds
            ? Emit(next, actorMemberId, completedAt, static (tenant, package, revision,
                actor, snapshot, occurredAt) => new AuditPackageBuilt(tenant, package,
                revision, actor, snapshot, occurredAt))
            : Emit(next, actorMemberId, completedAt, static (tenant, package, revision,
                actor, snapshot, occurredAt) => new AuditPackageBuildFailed(tenant,
                package, revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? RecordValidation(long expectedRevision, int packageVersion,
        string validationKey, IReadOnlyList<AuditPackageValidationFinding> findings,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset validatedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateHumanActor(actorMemberId, actorDisplay, validatedAt) is { } actorFailure)
            return actorFailure;
        var version = CurrentVersion(current);
        if (packageVersion != version.PackageVersion)
            return CommandFailure.StateConflict("Validation must target the current package version exactly.");
        if (version.Deliveries.Count > 0)
            return CommandFailure.StateConflict("A delivered package version cannot be revalidated; create an amendment.");
        if (LatestSuccessfulBuild(version) is not { } build)
            return CommandFailure.StateConflict("Validation requires a complete archive build.");
        validationKey = Clean(validationKey);
        if (!Token(validationKey, MaximumTokenLength) ||
            NormalizeFindings(findings) is not { } normalizedFindings)
            return Invalid("Validation requires a bounded key and valid, uniquely identified findings.");
        var validationId = Uuid.CreateVersion5(ValidationNamespace,
            $"{Id}\n{packageVersion}\n{validationKey}");
        if (version.Validations.FirstOrDefault(item => item.AuditPackageValidationId == validationId) is { } prior)
            return prior.ManifestId == version.Manifest.AuditPackageManifestId &&
                   prior.Findings.SequenceEqual(normalizedFindings) &&
                   prior.ValidatedByMemberId == actorMemberId &&
                   prior.ValidatedByDisplay == actorDisplay.Trim()
                ? null
                : CommandFailure.StateConflict("A validation key cannot be reused with changed result or attribution.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (version.Validations.Count >= MaximumValidations)
            return CommandFailure.StateConflict("The package reached its validation history limit.");

        var status = normalizedFindings.Any(item => item.BlocksDelivery)
            ? "blocked"
            : "passed";
        var validation = new AuditPackageValidationView(_tenantId, _programId, validationId,
            Id, packageVersion, version.Manifest.AuditPackageManifestId, version.Manifest.ManifestSha256,
            validationKey, status, version.Validations.Count + 1, validatedAt, actorMemberId,
            actorDisplay.Trim(), Freeze(normalizedFindings.ToArray()), []);
        var nextVersion = version with
        {
            Status = status == "blocked" ? "validation_blocked" : "validated",
            Validations = Append(version.Validations, validation),
        };
        var packageStatus = status == "blocked" ? "validation_blocked" : "validated";
        var next = ReplaceCurrentVersion(current, nextVersion, packageStatus, validatedAt);
        return Emit(next, actorMemberId, validatedAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageValidated(tenant, package,
            revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? ApproveValidationOverride(long expectedRevision, int packageVersion,
        Uuid findingId, string rationale, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset approvedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateHumanActor(actorMemberId, actorDisplay, approvedAt) is { } actorFailure)
            return actorFailure;
        var version = CurrentVersion(current);
        if (packageVersion != version.PackageVersion)
            return CommandFailure.StateConflict("An override must target the current package version.");
        if (version.Validations.Count == 0 || version.Validations[^1] is not { } validation ||
            validation.Status is not ("blocked" or "passed_with_overrides"))
            return CommandFailure.StateConflict("A validation override requires a blocked current validation.");
        var finding = validation.Findings.FirstOrDefault(item => item.FindingId == findingId);
        if (finding is null)
            return CommandFailure.MissingRecord("The validation finding was not found.");
        if (!finding.BlocksDelivery || !finding.OverrideAllowed)
            return CommandFailure.StateConflict("This blocking finding cannot be overridden.");
        if (actorMemberId == validation.ValidatedByMemberId)
            return CommandFailure.StateConflict("The validator cannot approve an override of their own finding.");
        rationale = Clean(rationale);
        if (!Bounded(rationale, 1, MaximumTextLength))
            return Invalid("A validation override requires a bounded rationale.");
        if (validation.Overrides.FirstOrDefault(item => item.FindingId == findingId) is { } existing)
            return existing.Rationale == rationale && existing.ApprovedByMemberId == actorMemberId &&
                   existing.ApprovedByDisplay == actorDisplay.Trim()
                ? null
                : CommandFailure.StateConflict("A validation finding can have only one immutable override.");
        if (expectedRevision != Revision)
            return StaleRevision();

        var overrides = Append(validation.Overrides, new AuditPackageValidationOverrideView(
            findingId, rationale, actorMemberId, actorDisplay.Trim(), approvedAt));
        var allBlockingFindingsOverridden = validation.Findings.Where(item => item.BlocksDelivery)
            .All(item => overrides.Any(approved => approved.FindingId == item.FindingId));
        var updatedValidation = validation with
        {
            Status = allBlockingFindingsOverridden ? "passed_with_overrides" : "blocked",
            Revision = validation.Revision + 1,
            UpdatedAt = approvedAt,
            Overrides = overrides,
        };
        var nextVersion = version with
        {
            Status = allBlockingFindingsOverridden ? "validated_with_overrides" : "validation_blocked",
            Validations = Replace(version.Validations, version.Validations.Count - 1,
                updatedValidation),
        };
        var next = ReplaceCurrentVersion(current, nextVersion,
            allBlockingFindingsOverridden ? "validated_with_overrides" : "validation_blocked",
            approvedAt);
        return Emit(next, actorMemberId, approvedAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageValidationOverrideApproved(
            tenant, package, revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? ApproveDisclosure(long expectedRevision, int packageVersion,
        string recipient, string purpose, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset approvedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateHumanActor(actorMemberId, actorDisplay, approvedAt) is { } actorFailure)
            return actorFailure;
        var version = CurrentVersion(current);
        if (packageVersion != version.PackageVersion)
            return CommandFailure.StateConflict("Disclosure must target the current package version.");
        if (!CanDeliver(version))
            return CommandFailure.StateConflict("Disclosure requires a complete build and a passed or fully overridden validation.");
        recipient = Clean(recipient);
        purpose = Clean(purpose);
        rationale = Clean(rationale);
        if (!Bounded(recipient, 1, MaximumTextLength) || !Bounded(purpose, 1, MaximumTextLength) ||
            !Bounded(rationale, 1, MaximumTextLength))
            return Invalid("Disclosure approval requires a recipient, purpose, and rationale.");
        var id = Uuid.CreateVersion5(DisclosureNamespace,
            $"{Id}\n{packageVersion}\n{recipient}\n{purpose}");
        if (version.DisclosureApprovals.FirstOrDefault(item => item.DisclosureApprovalId == id) is { } prior)
            return prior.Rationale == rationale && prior.ApprovedByMemberId == actorMemberId &&
                   prior.ApprovedByDisplay == actorDisplay.Trim()
                ? null
                : CommandFailure.StateConflict("A disclosure approval identity cannot be reused with changed intent.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (version.DisclosureApprovals.Count >= MaximumDisclosures)
            return CommandFailure.StateConflict("The package reached its disclosure approval limit.");

        var approval = new AuditPackageDisclosureApprovalView(id, packageVersion,
            version.Manifest.AuditPackageManifestId, version.Manifest.ManifestSha256, recipient,
            purpose, rationale, actorMemberId, actorDisplay.Trim(), approvedAt);
        var nextVersion = version with
        {
            Status = "disclosure_approved",
            DisclosureApprovals = Append(version.DisclosureApprovals, approval),
        };
        var next = ReplaceCurrentVersion(current, nextVersion, "disclosure_approved", approvedAt);
        return Emit(next, actorMemberId, approvedAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageDisclosureApproved(tenant,
            package, revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? RecordDelivery(long expectedRevision, int packageVersion,
        string recipient, string deliveryReference, Uuid? actorMemberId,
        string actorDisplay, DateTimeOffset deliveredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateActor(actorMemberId, actorDisplay, deliveredAt) is { } actorFailure)
            return actorFailure;
        var version = CurrentVersion(current);
        if (packageVersion != version.PackageVersion || !CanDeliver(version))
            return CommandFailure.StateConflict("Delivery must target a currently built and validated package version.");
        recipient = Clean(recipient);
        deliveryReference = Clean(deliveryReference);
        if (!Bounded(recipient, 1, MaximumTextLength) ||
            !Bounded(deliveryReference, 1, MaximumTextLength))
            return Invalid("A delivery requires an exact recipient and provider reference.");
        var approval = version.DisclosureApprovals.FirstOrDefault(item =>
            item.PackageVersion == packageVersion && item.ManifestId == version.Manifest.AuditPackageManifestId &&
            item.ManifestSha256 == version.Manifest.ManifestSha256 &&
            StringComparer.Ordinal.Equals(item.Recipient, recipient));
        if (approval is null)
            return CommandFailure.StateConflict("Delivery requires disclosure approval for this exact version and recipient.");
        var build = version.BuildAttempts.LastOrDefault(item => item.Status == "built");
        if (build?.ArchiveArtifactId is not { } archiveId || build.ArchiveSha256 is not { } archiveHash)
            return CommandFailure.StateConflict("The exact package archive is no longer available for delivery.");
        var deliveryId = Uuid.CreateVersion5(DeliveryNamespace,
            $"{Id}\n{packageVersion}\n{recipient}\n{deliveryReference}");
        if (version.Deliveries.FirstOrDefault(item => item.AuditPackageDeliveryId == deliveryId) is { } existing)
            return existing.Recipient == recipient && existing.DeliveryReference == deliveryReference &&
                   existing.ArchiveArtifactId == archiveId && existing.ArchiveSha256 == archiveHash &&
                   existing.ActorMemberId == actorMemberId && existing.ActorDisplay == Clean(actorDisplay) &&
                   existing.UpdatedAt == deliveredAt
                ? null
                : CommandFailure.StateConflict("A delivery reference cannot be reused with changed package content.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (version.Deliveries.Count >= MaximumDeliveries)
            return CommandFailure.StateConflict("The package reached its retained delivery limit.");

        var delivery = new AuditPackageDeliveryView(_tenantId, _programId, deliveryId,
            Id, packageVersion, version.Manifest.AuditPackageManifestId, version.Manifest.ManifestSha256,
            archiveId, archiveHash, recipient, deliveryReference, "delivered",
            version.Deliveries.Count + 1, deliveredAt, actorMemberId, Clean(actorDisplay));
        var nextVersion = version with
        {
            Status = "delivered",
            Deliveries = Append(version.Deliveries, delivery),
        };
        var next = ReplaceCurrentVersion(current, nextVersion, "delivered", deliveredAt);
        return Emit(next, actorMemberId, deliveredAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageDelivered(tenant, package,
            revision, actor, snapshot, occurredAt));
    }

    public CommandFailure? Amend(long expectedRevision, string amendmentReason,
        Uuid sourceSnapshotId, string sourceSnapshotSha256,
        IReadOnlyList<AuditPackageSelectionEntry> selectedContent,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset amendedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The audit package was not found.");
        if (ValidateHumanActor(actorMemberId, actorDisplay, amendedAt) is { } actorFailure)
            return actorFailure;
        var prior = CurrentVersion(current);
        if (current.PackageKind == "type_ii" && prior.SourcePeriodId == Uuid.Empty)
            return CommandFailure.StateConflict(
                "This legacy Type II package has no owning period binding and cannot be amended safely.");
        amendmentReason = Clean(amendmentReason);
        if (!Bounded(amendmentReason, 1, MaximumTextLength) || sourceSnapshotId == Uuid.Empty ||
            !Sha256(sourceSnapshotSha256))
            return Invalid("An amendment requires a new frozen source snapshot and bounded reason.");
        var content = new AuditPackageContent(current.ExaminationId, current.PackageKind,
            prior.SourceSnapshotKind, sourceSnapshotId, NormalizeHash(sourceSnapshotSha256),
            current.Name, selectedContent, prior.SourcePeriodId);
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("An amendment requires an explicit valid selection from its frozen source snapshot.");
        if (prior.PackageVersion > 1 && prior.AmendsVersion == prior.PackageVersion - 1 &&
            prior.SourceSnapshotId == sourceSnapshotId &&
            prior.SourceSnapshotSha256 == normalized.SourceSnapshotSha256 &&
            prior.SelectedContent.SequenceEqual(normalized.SelectedContent) &&
            prior.AmendmentReason == amendmentReason && prior.CreatedByMemberId == actorMemberId &&
            prior.CreatedByDisplay == actorDisplay.Trim() && prior.CreatedAt == amendedAt)
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (!prior.BuildAttempts.Any(attempt => attempt.Status == "built"))
            return CommandFailure.StateConflict("An amendment requires a successfully generated predecessor version.");
        var versionNumber = prior.PackageVersion + 1;
        var manifest = CreateManifest(normalized, versionNumber, amendedAt);
        if (manifest.ManifestSha256 == prior.Manifest.ManifestSha256 &&
            sourceSnapshotId == prior.SourceSnapshotId &&
            StringComparer.Ordinal.Equals(normalized.SourceSnapshotSha256, prior.SourceSnapshotSha256))
            return CommandFailure.StateConflict("An amendment must change the frozen snapshot or selected manifest content.");
        var nextVersion = NewVersion(normalized, manifest, versionNumber,
            prior.PackageVersion, amendmentReason, actorMemberId, actorDisplay.Trim(), amendedAt);
        var next = current with
        {
            Status = "draft",
            Revision = Revision + 1,
            CurrentVersion = versionNumber,
            UpdatedAt = amendedAt,
            Versions = Append(current.Versions, nextVersion),
        };
        return Emit(next, actorMemberId, amendedAt, static (tenant, package, revision,
            actor, snapshot, occurredAt) => new AuditPackageAmended(tenant, package,
            revision, actor, snapshot, occurredAt));
    }

    void Apply(AuditPackageCreated ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, creating: true);

    void Apply(AuditPackageBuildRequested ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageBuildProgressRecorded ev) => ApplyState(ev.TenantId,
        ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageBuildFailed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageBuilt ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageValidated ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageValidationOverrideApproved ev) => ApplyState(ev.TenantId,
        ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageDisclosureApproved ev) => ApplyState(ev.TenantId,
        ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageDelivered ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditPackageAmended ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void ApplyState(Uuid tenantId, Uuid packageId, long revision, Uuid? actorMemberId,
        AuditPackageView state, DateTimeOffset occurredAt, bool creating = false)
    {
        if (tenantId != _tenantId || packageId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId ||
            state.AuditPackageId != Id || state.Revision != revision ||
            occurredAt == default || revision != Revision + 1 ||
            creating != (_current is null) || ValidateState(state) is not null ||
            actorMemberId == Uuid.Empty || _current is { } current && !ValidTransition(current, state))
            throw new InvalidOperationException("The audit package event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(AuditPackageView state, Uuid? actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, AuditPackageView, DateTimeOffset, DomainEvent> createEvent,
        bool creating = false)
    {
        if (state.Revision != Revision + 1 || !ValidTransitionForEmit(state, creating))
            return Invalid("The package transition does not advance exactly one valid revision.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (!Fits(ev))
            return Invalid("The complete audit package event exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    bool ValidTransitionForEmit(AuditPackageView state, bool creating) =>
        creating ? _current is null && ValidateState(state) is null
            : _current is { } current && ValidateState(state) is null && ValidTransition(current, state);

    static bool ValidTransition(AuditPackageView prior, AuditPackageView next)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.AuditPackageId != prior.AuditPackageId ||
            next.ExaminationId != prior.ExaminationId || next.PackageKind != prior.PackageKind ||
            next.Name != prior.Name || next.Versions.Count < prior.Versions.Count ||
            next.Versions.Count > prior.Versions.Count + 1 ||
            !prior.Versions.Take(prior.Versions.Count - 1).SequenceEqual(
                next.Versions.Take(prior.Versions.Count - 1)))
            return false;
        if (next.Versions.Count == prior.Versions.Count)
        {
            var oldVersion = CurrentVersion(prior);
            var newVersion = CurrentVersion(next);
            return oldVersion.PackageVersion == newVersion.PackageVersion &&
                   oldVersion.SourceSnapshotId == newVersion.SourceSnapshotId &&
                   oldVersion.SourceSnapshotKind == newVersion.SourceSnapshotKind &&
                   oldVersion.SourceSnapshotSha256 == newVersion.SourceSnapshotSha256 &&
                   oldVersion.SourcePeriodId == newVersion.SourcePeriodId &&
                   oldVersion.SelectedContent.SequenceEqual(newVersion.SelectedContent) &&
                   SameManifest(oldVersion.Manifest, newVersion.Manifest) &&
                   IsAppendOrLastUpdate(oldVersion.BuildAttempts, newVersion.BuildAttempts) &&
                   IsAppendOrLastUpdate(oldVersion.Validations, newVersion.Validations) &&
                   IsAppendOnly(oldVersion.DisclosureApprovals, newVersion.DisclosureApprovals) &&
                   IsAppendOnly(oldVersion.Deliveries, newVersion.Deliveries);
        }
        var appended = next.Versions[^1];
        return Enumerable.Range(0, prior.Versions.Count).All(index =>
                   SameVersion(prior.Versions[index], next.Versions[index])) &&
               appended.PackageVersion == prior.CurrentVersion + 1 &&
               appended.AmendsVersion == prior.CurrentVersion &&
               appended.BuildAttempts.Count == 0 && appended.Validations.Count == 0 &&
               appended.DisclosureApprovals.Count == 0 && appended.Deliveries.Count == 0;
    }

    static bool SameManifest(AuditPackageManifestView left, AuditPackageManifestView right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.AuditPackageManifestId == right.AuditPackageManifestId &&
        left.AuditPackageId == right.AuditPackageId && left.PackageVersion == right.PackageVersion &&
        left.SourceSnapshotId == right.SourceSnapshotId &&
        left.SourceSnapshotSha256 == right.SourceSnapshotSha256 &&
        left.ManifestSha256 == right.ManifestSha256 && left.CreatedAt == right.CreatedAt &&
        left.IncludedRecordIds.SequenceEqual(right.IncludedRecordIds) &&
        left.Entries.SequenceEqual(right.Entries);

    static bool SameVersion(AuditPackageVersionView left, AuditPackageVersionView right) =>
        left.PackageVersion == right.PackageVersion && left.Status == right.Status &&
        left.SourceSnapshotId == right.SourceSnapshotId &&
        left.SourceSnapshotKind == right.SourceSnapshotKind &&
        left.SourceSnapshotSha256 == right.SourceSnapshotSha256 &&
        left.SourcePeriodId == right.SourcePeriodId &&
        left.SelectedContent.SequenceEqual(right.SelectedContent) &&
        SameManifest(left.Manifest, right.Manifest) &&
        left.BuildAttempts.SequenceEqual(right.BuildAttempts) &&
        left.Validations.Count == right.Validations.Count &&
        left.Validations.Zip(right.Validations).All(pair => SameValidation(pair.First, pair.Second)) &&
        left.DisclosureApprovals.SequenceEqual(right.DisclosureApprovals) &&
        left.Deliveries.SequenceEqual(right.Deliveries) &&
        left.AmendsVersion == right.AmendsVersion &&
        left.AmendmentReason == right.AmendmentReason &&
        left.CreatedByMemberId == right.CreatedByMemberId &&
        left.CreatedByDisplay == right.CreatedByDisplay && left.CreatedAt == right.CreatedAt;

    static bool SameValidation(AuditPackageValidationView left, AuditPackageValidationView right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.AuditPackageValidationId == right.AuditPackageValidationId &&
        left.AuditPackageId == right.AuditPackageId && left.PackageVersion == right.PackageVersion &&
        left.ManifestId == right.ManifestId && left.ManifestSha256 == right.ManifestSha256 &&
        left.ValidationKey == right.ValidationKey && left.Status == right.Status &&
        left.Revision == right.Revision && left.UpdatedAt == right.UpdatedAt &&
        left.ValidatedByMemberId == right.ValidatedByMemberId &&
        left.ValidatedByDisplay == right.ValidatedByDisplay &&
        left.Findings.SequenceEqual(right.Findings) && left.Overrides.SequenceEqual(right.Overrides);

    static bool IsAppendOnly<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) =>
        prior.Count <= next.Count && prior.SequenceEqual(next.Take(prior.Count));

    static bool IsAppendOrLastUpdate<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) =>
        prior.Count <= next.Count && (prior.Count < next.Count || prior.Count == 0 ||
            prior.Take(prior.Count - 1).SequenceEqual(next.Take(prior.Count - 1)));

    static CommandFailure? ValidateState(AuditPackageView state)
    {
        if (state.ExaminationId == Uuid.Empty || !PackageKinds.Contains(state.PackageKind) ||
            !Bounded(state.Name, 1, MaximumNameLength) || state.CreatedByMemberId == Uuid.Empty ||
            !Bounded(state.CreatedByDisplay, 1, 200) || state.CreatedAt == default ||
            state.UpdatedAt == default || state.CurrentVersion < 1 || state.Versions is null ||
            state.Versions.Count != state.CurrentVersion ||
            state.Status is not ("draft" or "building" or "build_failed" or "built" or
                "validation_blocked" or "validated" or "validated_with_overrides" or
                "disclosure_approved" or "delivered"))
            return Invalid("The package state has invalid examination identity, scope, or version history.");
        for (var index = 0; index < state.Versions.Count; index++)
        {
            var version = state.Versions[index];
            if (version.PackageVersion != index + 1 || version.SourceSnapshotId == Uuid.Empty ||
                version.SourceSnapshotKind != (state.PackageKind == "type_i"
                    ? "type_i_baseline" : "type_ii_close_snapshot") ||
                state.PackageKind == "type_i" && version.SourcePeriodId != Uuid.Empty ||
                !Sha256(version.SourceSnapshotSha256) || version.SelectedContent is null ||
                version.SelectedContent.Count is < 1 or > MaximumSelectedContent ||
                NormalizeContent(ContentFor(state, version, version.SelectedContent)) is not { } normalized ||
                !normalized.SelectedContent.SequenceEqual(version.SelectedContent) ||
                version.Manifest is null || version.Manifest.PackageVersion != version.PackageVersion ||
                version.Manifest.TenantId != state.TenantId || version.Manifest.ProgramId != state.ProgramId ||
                version.Manifest.AuditPackageId != state.AuditPackageId ||
                version.Manifest.SourceSnapshotId != version.SourceSnapshotId ||
                version.Manifest.SourceSnapshotSha256 != version.SourceSnapshotSha256 ||
                !VersionMatchesManifest(state, version) ||
                !ValidBuildHistory(version) || !ValidValidationHistory(state, version) ||
                !ValidDisclosureHistory(state, version) || !ValidDeliveryHistory(state, version))
                return Invalid("The package version history is incomplete or inconsistent with its manifest.");
        }
        if (state.Versions[^1].Status != state.Status || state.CurrentVersion != state.Versions[^1].PackageVersion)
            return Invalid("The package status must describe its current version.");
        return null;
    }

    static bool VersionMatchesManifest(AuditPackageView root, AuditPackageVersionView version)
    {
        if (version.Manifest.Entries is null || version.Manifest.IncludedRecordIds is null ||
            version.Manifest.Entries.Count != version.SelectedContent.Count ||
            version.Manifest.AuditPackageManifestId == Uuid.Empty || !Sha256(version.Manifest.ManifestSha256))
            return false;
        var expected = version.SelectedContent.Select(entry => new AuditPackageManifestEntryView(
                entry, PathFor(root.ExaminationId, entry)))
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(version.Manifest.Entries))
            return false;
        var recordIds = expected.Select(entry => entry.Source.SourceRecordId).Distinct().Order().ToArray();
        if (!recordIds.SequenceEqual(version.Manifest.IncludedRecordIds))
            return false;
        var content = ContentFor(root, version, version.SelectedContent);
        var digest = ManifestDigest(content, expected);
        return StringComparer.Ordinal.Equals(digest, version.Manifest.ManifestSha256) &&
               version.Manifest.AuditPackageManifestId == ManifestId(root.AuditPackageId, version.PackageVersion, digest);
    }

    static bool ValidBuildHistory(AuditPackageVersionView version)
    {
        if (version.BuildAttempts is null || version.BuildAttempts.Count > MaximumBuildAttempts)
            return false;
        var ids = new HashSet<Uuid>();
        foreach (var attempt in version.BuildAttempts)
        {
            if (attempt.BuildAttemptId == Uuid.Empty || !ids.Add(attempt.BuildAttemptId) ||
                !Token(attempt.BuildKey, MaximumTokenLength) || attempt.TotalEntryCount != version.Manifest.Entries.Count ||
                attempt.CompletedEntryCount < 0 || attempt.CompletedEntryCount > attempt.TotalEntryCount ||
                attempt.RequestedAt == default || attempt.UpdatedAt < attempt.RequestedAt ||
                attempt.RequestedByMemberId == Uuid.Empty || !Bounded(attempt.RequestedByDisplay, 1, 200) ||
                attempt.Status is not ("requested" or "running" or "failed" or "built") ||
                attempt.Status == "built" && (attempt.CompletedEntryCount != attempt.TotalEntryCount ||
                    attempt.CompletedAt is null || attempt.ArchiveArtifactId is null ||
                    attempt.ArchiveSha256 is null || !Sha256(attempt.ArchiveSha256) ||
                    attempt.ArchiveBytes is null or <= 0 || attempt.FailureCode is not null ||
                    !Sha256(attempt.ResultFingerprint)) ||
                attempt.Status == "failed" && (attempt.CompletedAt is null ||
                    string.IsNullOrWhiteSpace(attempt.FailureCode) || !Sha256(attempt.ResultFingerprint) ||
                    attempt.ArchiveArtifactId is not null || attempt.ArchiveSha256 is not null ||
                    attempt.ArchiveBytes is not null) ||
                (attempt.Status is "requested" or "running") && (attempt.CompletedAt is not null ||
                    attempt.FailureCode is not null || attempt.ResultFingerprint is not null ||
                    attempt.ArchiveArtifactId is not null || attempt.ArchiveSha256 is not null ||
                    attempt.ArchiveBytes is not null))
                return false;
        }
        return true;
    }

    static bool ValidValidationHistory(AuditPackageView root, AuditPackageVersionView version)
    {
        if (version.Validations is null || version.Validations.Count > MaximumValidations)
            return false;
        var priorRevision = 0L;
        foreach (var validation in version.Validations)
        {
            if (validation.TenantId != root.TenantId || validation.ProgramId != root.ProgramId ||
                validation.AuditPackageId != root.AuditPackageId || validation.PackageVersion != version.PackageVersion ||
                validation.ManifestId != version.Manifest.AuditPackageManifestId ||
                validation.ManifestSha256 != version.Manifest.ManifestSha256 ||
                validation.Revision < 1 || validation.Revision < priorRevision ||
                validation.Findings is null || validation.Overrides is null ||
                validation.Findings.Count > MaximumValidationFindings ||
                validation.Status is not ("blocked" or "passed" or "passed_with_overrides") ||
                validation.Findings.Select(item => item.FindingId).Distinct().Count() != validation.Findings.Count ||
                validation.Overrides.Select(item => item.FindingId).Distinct().Count() != validation.Overrides.Count)
                return false;
            priorRevision = validation.Revision;
        }
        return true;
    }

    static bool ValidDisclosureHistory(AuditPackageView root, AuditPackageVersionView version) =>
        version.DisclosureApprovals is not null && version.DisclosureApprovals.Count <= MaximumDisclosures &&
        version.DisclosureApprovals.All(approval => approval.DisclosureApprovalId != Uuid.Empty &&
            approval.PackageVersion == version.PackageVersion && approval.ManifestId == version.Manifest.AuditPackageManifestId &&
            approval.ManifestSha256 == version.Manifest.ManifestSha256 && approval.ApprovedByMemberId != Uuid.Empty &&
            Bounded(approval.Recipient, 1, MaximumTextLength) && Bounded(approval.Purpose, 1, MaximumTextLength) &&
            Bounded(approval.Rationale, 1, MaximumTextLength) && approval.ApprovedAt != default);

    static bool ValidDeliveryHistory(AuditPackageView root, AuditPackageVersionView version) =>
        version.Deliveries is not null && version.Deliveries.Count <= MaximumDeliveries &&
        version.Deliveries.All(delivery => delivery.TenantId == root.TenantId &&
            delivery.ProgramId == root.ProgramId && delivery.AuditPackageId == root.AuditPackageId &&
            delivery.PackageVersion == version.PackageVersion && delivery.ManifestId == version.Manifest.AuditPackageManifestId &&
            delivery.ManifestSha256 == version.Manifest.ManifestSha256 && delivery.ArchiveArtifactId != Uuid.Empty &&
            Sha256(delivery.ArchiveSha256) && delivery.Status == "delivered" &&
            Bounded(delivery.Recipient, 1, MaximumTextLength) &&
            Bounded(delivery.DeliveryReference, 1, MaximumTextLength) && delivery.UpdatedAt != default);

    static string? ValidateBuildOutput(AuditPackageVersionView version,
        AuditPackageBuildAttemptView attempt, AuditPackageBuildOutput? output)
    {
        if (output is null)
            return "invalid_build_result";
        if (!string.IsNullOrWhiteSpace(output.FailureCode))
            return Token(Clean(output.FailureCode), MaximumTokenLength)
                ? Clean(output.FailureCode)
                : "worker_reported_failure";
        if (output.ManifestId != version.Manifest.AuditPackageManifestId ||
            !StringComparer.Ordinal.Equals(output.ManifestSha256, version.Manifest.ManifestSha256))
            return "manifest_identity_mismatch";
        if (output.IncludedEntries is null || !version.Manifest.Entries.SequenceEqual(output.IncludedEntries))
            return "incomplete_or_inconsistent_entries";
        if (attempt.CompletedEntryCount > attempt.TotalEntryCount ||
            output.ArchiveArtifactId is not { } artifactId || artifactId == Uuid.Empty ||
            !Sha256(output.ArchiveSha256) || output.ArchiveBytes is not > 0)
            return "invalid_archive_identity";
        return null;
    }

    static AuditPackageContent? NormalizeContent(AuditPackageContent? content)
    {
        if (content is null || content.ExaminationId == Uuid.Empty ||
            content.SourceSnapshotId == Uuid.Empty || !Sha256(content.SourceSnapshotSha256) ||
            !PackageKinds.Contains(content.PackageKind) ||
            content.PackageKind == "type_i" && content.SourceSnapshotKind != "type_i_baseline" ||
            content.PackageKind == "type_ii" && content.SourceSnapshotKind != "type_ii_close_snapshot" ||
            !Bounded(content.Name, 1, MaximumNameLength) || content.SelectedContent is null ||
            content.SelectedContent.Count is < 1 or > MaximumSelectedContent)
            return null;
        var entries = new List<AuditPackageSelectionEntry>(content.SelectedContent.Count);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in content.SelectedContent)
        {
            if (entry is null || !Token(entry.Section, 80) || !Token(entry.StableId, 160) ||
                !Token(entry.SourceType, 80) || entry.SourceRecordId == Uuid.Empty ||
                entry.SourceVersion <= 0 || !Token(entry.Extension, 16) ||
                !Sha256(entry.ContentSha256) || entry.ByteLength <= 0 ||
                !AuthorshipKinds.Contains(entry.Authorship))
                return null;
            var key = $"{entry.SourceType}\n{entry.SourceRecordId}\n{entry.SourceVersion}";
            if (!unique.Add(key))
                return null;
            var normalizedEntry = entry with
            {
                Section = Clean(entry.Section),
                StableId = Clean(entry.StableId),
                SourceType = Clean(entry.SourceType),
                Extension = Clean(entry.Extension).ToLowerInvariant(),
                ContentSha256 = NormalizeHash(entry.ContentSha256),
            };
            if (!paths.Add(PathFor(content.ExaminationId, normalizedEntry)))
                return null;
            entries.Add(normalizedEntry);
        }
        return content with
        {
            PackageKind = Clean(content.PackageKind),
            SourceSnapshotKind = Clean(content.SourceSnapshotKind),
            SourceSnapshotSha256 = NormalizeHash(content.SourceSnapshotSha256),
            Name = Clean(content.Name),
            SelectedContent = Freeze(entries.ToArray()),
        };
    }

    static ReadOnlyCollection<AuditPackageValidationFinding>? NormalizeFindings(
        IReadOnlyList<AuditPackageValidationFinding>? findings)
    {
        if (findings is null || findings.Count > MaximumValidationFindings)
            return null;
        var result = new List<AuditPackageValidationFinding>(findings.Count);
        foreach (var finding in findings)
        {
            if (finding is null || finding.FindingId == Uuid.Empty ||
                !Token(finding.Code, 100) || finding.Severity is not ("error" or "warning" or "info") ||
                finding.OverrideAllowed && !finding.BlocksDelivery ||
                !Bounded(finding.Summary, 1, MaximumTextLength))
                return null;
            result.Add(finding with { Code = Clean(finding.Code), Summary = Clean(finding.Summary) });
        }
        if (result.Select(item => item.FindingId).Distinct().Count() != result.Count)
            return null;
        return Freeze(result.OrderBy(item => item.FindingId).ToArray());
    }

    AuditPackageManifestView CreateManifest(AuditPackageContent content,
        int packageVersion, DateTimeOffset createdAt)
    {
        var entries = content.SelectedContent.Select(entry => new AuditPackageManifestEntryView(
                entry, PathFor(content.ExaminationId, entry)))
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToArray();
        var digest = ManifestDigest(content, entries);
        return new AuditPackageManifestView(_tenantId, _programId,
            ManifestId(Id, packageVersion, digest), Id,
            packageVersion, content.SourceSnapshotId, content.SourceSnapshotSha256,
            Freeze(entries.Select(entry => entry.Source.SourceRecordId).Distinct().Order().ToArray()),
            Freeze(entries), digest, createdAt);
    }

    AuditPackageManifestView BindManifest(AuditPackageManifestView manifest) => manifest with
    {
        TenantId = _tenantId,
        ProgramId = _programId,
        AuditPackageId = Id,
        AuditPackageManifestId = ManifestId(Id, manifest.PackageVersion, manifest.ManifestSha256),
    };

    static AuditPackageVersionView NewVersion(AuditPackageContent content,
        AuditPackageManifestView unboundManifest, int packageVersion, int? amendsVersion,
        string? amendmentReason, Uuid actorMemberId, string actorDisplay, DateTimeOffset createdAt)
    {
        var manifest = unboundManifest;
        return new AuditPackageVersionView(packageVersion, "draft", content.SourceSnapshotId,
            content.SourceSnapshotKind, content.SourceSnapshotSha256,
            Freeze(content.SelectedContent.ToArray()), manifest, [], [], [], [], amendsVersion,
            amendmentReason, actorMemberId, actorDisplay, createdAt, content.SourcePeriodId);
    }

    AuditPackageView ReplaceCurrentVersion(AuditPackageView current,
        AuditPackageVersionView version, string status, DateTimeOffset updatedAt)
    {
        var versions = current.Versions.ToArray();
        versions[^1] = version;
        return current with
        {
            Status = status,
            Revision = Revision + 1,
            UpdatedAt = updatedAt,
            Versions = Freeze(versions),
        };
    }

    static AuditPackageVersionView CurrentVersion(AuditPackageView state) => state.Versions[^1];

    AuditPackageView? CurrentOrMissing() => _current;

    static AuditPackageContent ContentFor(AuditPackageView root,
        AuditPackageVersionView version, IReadOnlyList<AuditPackageSelectionEntry> entries) =>
        new(root.ExaminationId, root.PackageKind, version.SourceSnapshotKind,
            version.SourceSnapshotId, version.SourceSnapshotSha256, root.Name, entries,
            version.SourcePeriodId);

    static string ManifestDigest(AuditPackageContent content,
        IReadOnlyList<AuditPackageManifestEntryView> entries)
    {
        var builder = new StringBuilder();
        builder.Append(content.PackageKind).Append('\n')
            .Append(content.ExaminationId).Append('\n')
            .Append(content.SourceSnapshotKind).Append('\n')
            .Append(content.SourceSnapshotId).Append('\n')
            .Append(content.SourceSnapshotSha256).Append('\n');
        foreach (var entry in entries)
        {
            var source = entry.Source;
            builder.Append(entry.RelativePath).Append('|')
                .Append(source.SourceType).Append('|')
                .Append(source.SourceRecordId).Append('|')
                .Append(source.SourceVersion.ToString(CultureInfo.InvariantCulture)).Append('|');
            if (source.SourceVersionId != Uuid.Empty)
                builder.Append(source.SourceVersionId).Append('|');
            builder.Append(source.ContentSha256).Append('|')
                .Append(source.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(source.Authorship).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    static string PathFor(Uuid examinationId, AuditPackageSelectionEntry entry) =>
        $"{examinationId}/{entry.Section}/{entry.StableId}_{entry.SourceVersion.ToString(CultureInfo.InvariantCulture)}.{entry.Extension.ToLowerInvariant()}";

    static Uuid ManifestId(Uuid packageId, int packageVersion, string digest) =>
        Uuid.CreateVersion5(ManifestNamespace, $"{packageId}\n{packageVersion}\n{digest}");

    static bool CanDeliver(AuditPackageVersionView version) =>
        LatestSuccessfulBuild(version) is { } build &&
        build.ArchiveArtifactId is not null && build.ArchiveSha256 is not null &&
        version.Validations.Count > 0 &&
        version.Validations[^1] is { Status: "passed" or "passed_with_overrides" } validation &&
        validation.ManifestId == version.Manifest.AuditPackageManifestId &&
        validation.ManifestSha256 == version.Manifest.ManifestSha256;

    static AuditPackageBuildAttemptView? LatestSuccessfulBuild(AuditPackageVersionView version)
    {
        for (var index = version.BuildAttempts.Count - 1; index >= 0; index--)
            if (version.BuildAttempts[index].Status == "built")
                return version.BuildAttempts[index];
        return null;
    }

    static int IndexOfAttempt(AuditPackageVersionView version, Uuid attemptId) =>
        version.BuildAttempts.ToList().FindIndex(attempt => attempt.BuildAttemptId == attemptId);

    static bool SameInitialContent(AuditPackageView current, AuditPackageContent? content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset createdAt)
    {
        if (current.CurrentVersion != 1 || NormalizeContent(content) is not { } normalized)
            return false;
        var version = current.Versions[0];
        return current.ExaminationId == normalized.ExaminationId &&
               current.PackageKind == normalized.PackageKind && current.Name == normalized.Name &&
               current.CreatedByMemberId == actorMemberId && current.CreatedByDisplay == actorDisplay.Trim() &&
               current.CreatedAt == createdAt && version.SourceSnapshotId == normalized.SourceSnapshotId &&
               version.SourceSnapshotKind == normalized.SourceSnapshotKind &&
               version.SourceSnapshotSha256 == normalized.SourceSnapshotSha256 &&
               version.SourcePeriodId == normalized.SourcePeriodId &&
               version.SelectedContent.SequenceEqual(normalized.SelectedContent);
    }

    static bool BuildResultAlreadyRecorded(AuditPackageBuildAttemptView attempt,
        AuditPackageBuildOutput? output) => attempt.Status is "built" or "failed" &&
        StringComparer.Ordinal.Equals(attempt.ResultFingerprint, BuildResultFingerprint(output));

    static string BuildResultFingerprint(AuditPackageBuildOutput? output) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(output)))
            .ToLowerInvariant();

    AuditPackageView Freeze(AuditPackageView state) => state with
    {
        Versions = Freeze(state.Versions.Select(Freeze).ToArray()),
    };

    AuditPackageVersionView Freeze(AuditPackageVersionView version) => version with
    {
        SelectedContent = Freeze(version.SelectedContent.ToArray()),
        Manifest = BindManifest(version.Manifest with
        {
            IncludedRecordIds = Freeze(version.Manifest.IncludedRecordIds.ToArray()),
            Entries = Freeze(version.Manifest.Entries.ToArray()),
        }),
        BuildAttempts = Freeze(version.BuildAttempts.ToArray()),
        Validations = Freeze(version.Validations.Select(validation => validation with
        {
            Findings = Freeze(validation.Findings.ToArray()),
            Overrides = Freeze(validation.Overrides.ToArray()),
        }).ToArray()),
        DisclosureApprovals = Freeze(version.DisclosureApprovals.ToArray()),
        Deliveries = Freeze(version.Deliveries.ToArray()),
    };

    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> source, T item) =>
        Freeze(source.Append(item).ToArray());

    static ReadOnlyCollection<T> Replace<T>(IReadOnlyList<T> source, int index, T item)
    {
        var result = source.ToArray();
        result[index] = item;
        return Freeze(result);
    }

    static ReadOnlyCollection<T> Freeze<T>(T[] items) => Array.AsReadOnly(items);

    static string Clean(string? value) => value?.Trim() ?? string.Empty;

    static bool Token(string? value, int maximumLength)
    {
        value = Clean(value);
        if (value.Length is < 1 || value.Length > maximumLength)
            return false;
        return value.All(character => character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
    }

    static bool Bounded(string? value, int minimumLength, int maximumLength)
    {
        value = Clean(value);
        return value.Length >= minimumLength && value.Length <= maximumLength &&
               !value.Any(char.IsControl);
    }

    static bool Sha256(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    static string NormalizeHash(string? value) => Clean(value).ToLowerInvariant();

    static CommandFailure? ValidateHumanActor(Uuid actorMemberId, string actorDisplay,
        DateTimeOffset occurredAt) => actorMemberId == Uuid.Empty || occurredAt == default ||
        !Bounded(actorDisplay, 1, 200)
        ? CommandFailure.InvalidContent("An audit package decision requires an attributed member and timestamp.")
        : null;

    static CommandFailure? ValidateActor(Uuid? actorMemberId, string actorDisplay,
        DateTimeOffset occurredAt) => occurredAt == default || !Bounded(actorDisplay, 1, 200) ||
        actorMemberId == Uuid.Empty || actorMemberId is null &&
        actorDisplay.Trim() is not ("system" or "build-worker")
        ? CommandFailure.InvalidContent("An audit package action requires a member or identified system actor and timestamp.")
        : null;

    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("audit package", Revision));

    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);

    static bool Fits(DomainEvent ev) => DomainEventPayload.Size(ev) <= MaximumEventBytes;
}
