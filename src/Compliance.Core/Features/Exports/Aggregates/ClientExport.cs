using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>
/// Owns one tenant export job, its exact included/withheld source inventory,
/// retryable archive builds, attest-package delivery decisions, and final receipt.
/// </summary>
public sealed class ClientExport : Aggregate
{
    const int MaximumRecords = 20000;
    const int MaximumAttestPackages = 256;
    const int MaximumBuildAttempts = 32;
    const int MaximumAttestDeliveryAttempts = 32;
    const int MaximumTextLength = 4000;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    static readonly HashSet<string> Scopes = new(StringComparer.Ordinal)
    {
        "organization", "engagement_collaboration"
    };
    static readonly HashSet<string> WithheldReasonCodes = new(StringComparer.Ordinal)
    {
        "withheld_authorization", "withheld_retention", "withheld_engagement_hold",
        "withheld_legal_hold", "withheld_redacted", "withheld_unavailable"
    };

    readonly Uuid _tenantId;
    ClientExportView? _current;

    public long Revision => _current?.Revision ?? 0;
    public ClientExportView? Current => _current;

    public ClientExport(Uuid tenantId, Uuid exportId)
        : base(exportId, new EventStreamAddress(tenantId.ToString(), "client-exports",
            exportId.ToString()))
    {
        _tenantId = tenantId;
        On<ClientExportRequested>(Apply);
        On<ClientExportBuilt>(Apply);
        On<ClientExportBuildFailed>(Apply);
        On<ClientExportAttestPackageDeliveryRecorded>(Apply);
        On<ClientExportDelivered>(Apply);
    }

    public CommandFailure? Request(ClientExportContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset requestedAt)
    {
        if (_current is { } existing)
            return SameRequest(existing, content, actorMemberId, actorDisplay, requestedAt) ? null :
                CommandFailure.StateConflict("The client export identity already has a different scope or source snapshot.");
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty ||
            !ValidActor(actorMemberId, actorDisplay, requestedAt) || NormalizeContent(content) is not { } normalized)
            return Invalid("A client export requires a valid tenant, exact source inventory, scope, and attributed requester.");
        var digest = Digest(normalized);
        var state = new ClientExportView(_tenantId, Id, "requested", 1, normalized, digest,
            actorMemberId, actorDisplay.Trim(), requestedAt, [], [], null, false,
            actorMemberId, actorDisplay.Trim(), requestedAt);
        return Emit(state, actorMemberId, requestedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ClientExportRequested(tenant, subject, revision, actor, snapshot, at),
            "request", creating: true);
    }

    public CommandFailure? RecordBuildResult(long expectedRevision,
        ClientExportBuildResultContent result, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset completedAt) =>
        RecordBuildResult(expectedRevision, result, actorMemberId,
            ActorReference.ForMember(actorMemberId, actorDisplay), completedAt);

    public CommandFailure? RecordBuildResult(long expectedRevision,
        ClientExportBuildResultContent result, ActorReference completedBy,
        DateTimeOffset completedAt) =>
        RecordBuildResult(expectedRevision, result, null, completedBy, completedAt);

    CommandFailure? RecordBuildResult(long expectedRevision,
        ClientExportBuildResultContent result, Uuid? actorMemberId,
        ActorReference completedBy, DateTimeOffset completedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The client export was not found.");
        if (result is not null && current.BuildAttempts.FirstOrDefault(item => item.AttemptId == result.AttemptId) is { } prior)
            return SameBuildAttempt(prior, result, completedBy, completedAt)
                ? null : CommandFailure.StateConflict("A client export build-attempt identity cannot be reused.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status is not ("requested" or "build_failed") ||
            current.BuildAttempts.Count >= MaximumBuildAttempts ||
            !ValidStateActor(actorMemberId, completedBy, completedBy.Display, completedAt) ||
            completedAt < current.UpdatedAt ||
            NormalizeBuildAttempt(result, current, actorMemberId, completedBy, completedAt) is not { } attempt ||
            attempt.AttemptNumber != current.BuildAttempts.Count + 1)
            return Invalid("An export build result must report the next attempt against the exact frozen source inventory.");
        var state = current with
        {
            Status = attempt.Outcome == "built" ? "built" : "build_failed",
            Revision = Revision + 1,
            BuildAttempts = Append(current.BuildAttempts, attempt),
            ActorMemberId = actorMemberId,
            ActorDisplay = completedBy.Display.Trim(),
            UpdatedAt = completedAt,
            StoredActor = actorMemberId is null ? completedBy : null,
        };
        if (attempt.Outcome == "built")
            return Emit(state, actorMemberId, completedAt,
                static (tenant, subject, revision, actor, snapshot, at) =>
                    new ClientExportBuilt(tenant, subject, revision, actor, snapshot, at), "built");
        return Emit(state, actorMemberId, completedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ClientExportBuildFailed(tenant, subject, revision, actor, snapshot, at), "build_failed");
    }

    public CommandFailure? RecordAttestPackageDelivery(long expectedRevision, Uuid packageId,
        string decision, string rationale, string? recipient, string? deliveryReference,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The client export was not found.");
        decision = Clean(decision).ToLowerInvariant();
        rationale = Clean(rationale);
        recipient = Clean(recipient);
        deliveryReference = Clean(deliveryReference);
        var required = current.Content.RequiredAttestPackages.FirstOrDefault(item => item.PackageId == packageId);
        if (required is null)
            return CommandFailure.MissingRecord("The attest collaboration package was not required by this export.");
        var prior = current.AttestPackageDeliveries.LastOrDefault(item => item.PackageId == packageId);
        if (prior is not null && prior.Decision == decision && prior.Rationale == rationale && prior.Recipient == NullIfEmpty(recipient) &&
                prior.DeliveryReference == NullIfEmpty(deliveryReference) && prior.RecordedByMemberId == actorMemberId &&
                prior.RecordedByDisplay == Clean(actorDisplay) && prior.RecordedAt == recordedAt
                )
            return null;
        if (prior?.Decision == "delivered")
            return CommandFailure.StateConflict("A successful attest-package delivery decision is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "built" || decision is not ("delivered" or "not_delivered") ||
            current.AttestPackageDeliveries.Count(item => item.PackageId == packageId) >= MaximumAttestDeliveryAttempts ||
            !Bounded(rationale, 1, MaximumTextLength) || !ValidActor(actorMemberId, actorDisplay, recordedAt) ||
            recordedAt < current.UpdatedAt ||
            decision == "delivered" && (!Bounded(recipient, 1, 300) || !Bounded(deliveryReference, 1, 500)))
            return CommandFailure.StateConflict("An attest package delivery decision requires a successfully built export and retained receipt or rationale.");
        var delivery = new ClientExportAttestDeliveryView(required.EngagementId, required.PackageId,
            required.ManifestSha256, decision, rationale, NullIfEmpty(recipient),
            NullIfEmpty(deliveryReference), actorMemberId, actorDisplay.Trim(), recordedAt);
        var state = current with
        {
            Revision = Revision + 1,
            AttestPackageDeliveries = Append(current.AttestPackageDeliveries, delivery),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = recordedAt,
        };
        return Emit(state, actorMemberId, recordedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ClientExportAttestPackageDeliveryRecorded(tenant, subject, revision, actor, snapshot, at),
            "attest_delivery");
    }

    public CommandFailure? Deliver(long expectedRevision, string recipient,
        string deliveryReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset deliveredAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The client export was not found.");
        recipient = Clean(recipient);
        deliveryReference = Clean(deliveryReference);
        if (current.Delivery is { } prior)
            return prior.Recipient == recipient && prior.DeliveryReference == deliveryReference &&
                prior.RecordedByMemberId == actorMemberId && prior.RecordedByDisplay == Clean(actorDisplay) &&
                prior.DeliveredAt == deliveredAt ? null :
                CommandFailure.StateConflict("The delivered client export receipt is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "built" || current.BuildAttempts.Count == 0 ||
            current.BuildAttempts[^1] is not { Outcome: "built", ArchiveArtifactId: { } archiveId, ArchiveSha256: { } archiveHash } ||
            current.Content.RequiredAttestPackages.Any(package =>
                !HasLatestSuccessfulAttestDelivery(current.AttestPackageDeliveries, package)) ||
            !Bounded(recipient, 1, 300) || !Bounded(deliveryReference, 1, 500) ||
            !ValidActor(actorMemberId, actorDisplay, deliveredAt) || deliveredAt < current.UpdatedAt)
            return CommandFailure.StateConflict("A complete build and every required attest-package delivery decision must precede final export delivery.");
        var delivery = new ClientExportDeliveryView(recipient, deliveryReference,
            archiveId, archiveHash, actorMemberId, actorDisplay.Trim(), deliveredAt);
        var state = current with
        {
            Status = "delivered",
            Revision = Revision + 1,
            Delivery = delivery,
            IsComplete = true,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = deliveredAt,
        };
        return Emit(state, actorMemberId, deliveredAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ClientExportDelivered(tenant, subject, revision, actor, snapshot, at),
            "delivered");
    }

    void Apply(ClientExportRequested ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "request");
    void Apply(ClientExportBuilt ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "built");
    void Apply(ClientExportBuildFailed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "build_failed");
    void Apply(ClientExportAttestPackageDeliveryRecorded ev) => ApplyState(ev.TenantId,
        ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "attest_delivery");
    void Apply(ClientExportDelivered ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "delivered");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        ClientExportView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null || state.TenantId != _tenantId ||
            state.ClientExportId != Id || state.Revision != revision || state.UpdatedAt != occurredAt ||
            occurredAt == default || revision != Revision + 1 || creating != (_current is null) ||
            actorMemberId == Uuid.Empty || state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "request" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The client-export event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(ClientExportView state, Uuid? actorMemberId,
        DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, ClientExportView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null || !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The client-export transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (DomainEventPayload.Size(ev) > MaximumEventBytes)
            return Invalid("The complete client-export state exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(ClientExportView state)
    {
        if (state.TenantId != _tenantId || state.ClientExportId != Id || state.Revision <= 0 ||
            NormalizeContent(state.Content) is not { } content || Digest(content) != state.ContentSha256 ||
            state.BuildAttempts is null || state.BuildAttempts.Count > MaximumBuildAttempts ||
            state.AttestPackageDeliveries is null ||
            state.AttestPackageDeliveries.Count > content.RequiredAttestPackages.Count * MaximumAttestDeliveryAttempts ||
            state.BuildAttempts.Where((attempt, index) => !ValidBuildAttempt(attempt, state) ||
                attempt.AttemptNumber != index + 1).Any() ||
            state.BuildAttempts.Select(item => item.AttemptId).Distinct().Count() != state.BuildAttempts.Count ||
            state.AttestPackageDeliveries.Any(item => !ValidAttestDelivery(item, content)) ||
            !ValidAttestDeliveryHistory(state.AttestPackageDeliveries) ||
            state.Delivery is { } delivery && !ValidDelivery(delivery, state) ||
            state.IsComplete != (state.Status == "delivered" && state.Delivery is not null) ||
            !ValidStatus(state) || !ValidActor(state.RequestedByMemberId, state.RequestedByDisplay, state.RequestedAt) ||
            !ValidStateActor(state.ActorMemberId, state.StoredActor, state.ActorDisplay, state.UpdatedAt))
            return Invalid("The client-export event contains an invalid state snapshot.");
        return null;
    }

    static bool ValidTransition(ClientExportView prior, ClientExportView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ClientExportId != prior.ClientExportId || next.ContentSha256 != prior.ContentSha256 ||
            Digest(next.Content) != Digest(prior.Content) || next.RequestedByMemberId != prior.RequestedByMemberId ||
            next.RequestedAt != prior.RequestedAt || !Prefix(prior.BuildAttempts, next.BuildAttempts) ||
            !Prefix(prior.AttestPackageDeliveries, next.AttestPackageDeliveries))
            return false;
        return transition switch
        {
            "built" => prior.Status is "requested" or "build_failed" && next.Status == "built" &&
                next.BuildAttempts.Count == prior.BuildAttempts.Count + 1 && next.BuildAttempts[^1].Outcome == "built" &&
                next.BuildAttempts[^1].CompletedByMemberId == actor &&
                next.BuildAttempts[^1].EffectiveCompletedBy == next.EffectiveActor &&
                next.BuildAttempts[^1].CompletedAt == next.UpdatedAt &&
                next.Delivery is null,
            "build_failed" => prior.Status is "requested" or "build_failed" && next.Status == "build_failed" &&
                next.BuildAttempts.Count == prior.BuildAttempts.Count + 1 && next.BuildAttempts[^1].Outcome == "failed" &&
                next.BuildAttempts[^1].CompletedByMemberId == actor &&
                next.BuildAttempts[^1].EffectiveCompletedBy == next.EffectiveActor &&
                next.BuildAttempts[^1].CompletedAt == next.UpdatedAt &&
                next.Delivery is null,
            "attest_delivery" => prior.Status == "built" && next.Status == "built" &&
                next.BuildAttempts.Count == prior.BuildAttempts.Count &&
                next.AttestPackageDeliveries.Count == prior.AttestPackageDeliveries.Count + 1 &&
                next.AttestPackageDeliveries[^1].RecordedByMemberId == actor &&
                next.AttestPackageDeliveries[^1].RecordedAt == next.UpdatedAt && next.Delivery is null,
            "delivered" => prior.Status == "built" && next.Status == "delivered" &&
                next.BuildAttempts.Count == prior.BuildAttempts.Count &&
                next.AttestPackageDeliveries.Count == prior.AttestPackageDeliveries.Count &&
                next.Delivery is { } delivery && delivery.RecordedByMemberId == actor &&
                delivery.DeliveredAt == next.UpdatedAt,
            _ => false,
        };
    }

    static ClientExportContent? NormalizeContent(ClientExportContent? content)
    {
        if (content is null)
            return null;
        var scope = Clean(content.Scope).ToLowerInvariant();
        var format = Clean(content.Format).ToLowerInvariant();
        if (!Scopes.Contains(scope) || !StringComparer.Ordinal.Equals(format, "zip") ||
            !Bounded(content.ExportKey, 1, 200) ||
            content.SourceSnapshotId == Uuid.Empty || !Sha256(content.SourceSnapshotSha256) ||
            content.ExpectedRecordCount is < 1 or > MaximumRecords || content.Records is null ||
            content.Records.Count != content.ExpectedRecordCount || content.RequiredAttestPackages is null ||
            content.RequiredAttestPackages.Count > MaximumAttestPackages ||
            scope == "engagement_collaboration" && content.RequiredAttestPackages.Count == 0 ||
            scope == "organization" && content.RequiredAttestPackages.Count > 0 ||
            content.Records.Any(item => !ValidRecord(item)) ||
            content.Records.Select(item => (item.RecordType, item.RecordId, item.Version)).Distinct().Count() != content.Records.Count ||
            content.RequiredAttestPackages.Any(item => !ValidPackage(item)) ||
            content.RequiredAttestPackages.Select(item => item.PackageId).Distinct().Count() != content.RequiredAttestPackages.Count)
            return null;
        return content with
        {
            Scope = scope,
            Format = format,
            ExportKey = content.ExportKey.Trim(),
            SourceSnapshotSha256 = NormalizeHash(content.SourceSnapshotSha256),
            Records = Freeze(content.Records.Select(item => item with
            {
                RecordType = item.RecordType.Trim().ToLowerInvariant(),
                ContentSha256 = NormalizeHash(item.ContentSha256),
                Disposition = item.Disposition.Trim().ToLowerInvariant(),
                WithheldReasonCode = NullIfEmpty(item.WithheldReasonCode)?.ToLowerInvariant(),
                WithheldReason = NullIfEmpty(item.WithheldReason),
                ArtifactIds = Freeze(item.ArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            }).OrderBy(item => item.RecordType, StringComparer.Ordinal)
                .ThenBy(item => IdKey(item.RecordId), StringComparer.Ordinal)
                .ThenBy(item => item.Version).ToArray()),
            RequiredAttestPackages = Freeze(content.RequiredAttestPackages.OrderBy(item => IdKey(item.PackageId), StringComparer.Ordinal).ToArray()),
        };
    }

    static ClientExportBuildAttemptView? NormalizeBuildAttempt(ClientExportBuildResultContent? result,
        ClientExportView state, Uuid? actorMemberId, ActorReference actor, DateTimeOffset at)
    {
        if (result is null || result.AttemptId == Uuid.Empty || result.AttemptNumber <= 0 ||
            result.Outcome is not ("built" or "failed") || result.IncludedRecordCount < 0 ||
            result.WithheldRecordCount < 0 || result.SourceSnapshotId != state.Content.SourceSnapshotId ||
            NormalizeHash(result.SourceSnapshotSha256) != state.Content.SourceSnapshotSha256 ||
            !ValidStateActor(actorMemberId, actor, actor.Display, at))
            return null;
        var success = result.Outcome == "built";
        if (success && (result.ArchiveArtifactId is null || result.ArchiveArtifactId == Uuid.Empty ||
                !Sha256(result.ArchiveSha256) || result.ManifestId is null || result.ManifestId == Uuid.Empty ||
                result.FailureCode is not null ||
                result.IncludedRecordCount != state.Content.Records.Count(item => item.Disposition == "included") ||
                result.WithheldRecordCount != state.Content.Records.Count(item => item.Disposition == "withheld")))
            return null;
        if (!success && (result.ArchiveArtifactId is not null || result.ArchiveSha256 is not null ||
                result.ManifestId is not null || result.FailureCode is not { } code || !Token(code, 100)))
            return null;
        return new ClientExportBuildAttemptView(result.AttemptId, result.AttemptNumber,
            result.Outcome, result.ArchiveArtifactId, result.ArchiveSha256 is null ? null : NormalizeHash(result.ArchiveSha256),
            result.FailureCode?.Trim().ToLowerInvariant(), result.ManifestId,
            result.IncludedRecordCount, result.WithheldRecordCount, result.SourceSnapshotId,
            state.Content.SourceSnapshotSha256, actorMemberId, actor.Display.Trim(), at,
            actorMemberId is null ? actor : null);
    }

    static bool ValidRecord(ClientExportRecordContent item)
    {
        if (item is null || !Token(item.RecordType, 100) || item.RecordId == Uuid.Empty || item.Version <= 0 ||
            !Sha256(item.ContentSha256) || item.Disposition is not ("included" or "withheld") ||
            item.ArtifactIds is null || item.ArtifactIds.Count > 256 || !ValidIds(item.ArtifactIds))
            return false;
        return item.Disposition == "included"
            ? item.WithheldReasonCode is null && item.WithheldReason is null
            : item.WithheldReasonCode is { } code && WithheldReasonCodes.Contains(code) &&
              Bounded(item.WithheldReason, 1, MaximumTextLength);
    }

    static bool ValidPackage(ClientExportAttestPackageContent item) => item is not null &&
        item.EngagementId != Uuid.Empty && item.PackageId != Uuid.Empty && Sha256(item.ManifestSha256) &&
        item.ItemCount > 0 && item.ItemCount <= MaximumRecords;

    static bool ValidBuildAttempt(ClientExportBuildAttemptView item, ClientExportView state) =>
        item.AttemptId != Uuid.Empty && item.AttemptNumber > 0 && (item.Outcome is "built" or "failed") &&
        item.IncludedRecordCount >= 0 && item.WithheldRecordCount >= 0 &&
        item.SourceSnapshotId == state.Content.SourceSnapshotId &&
        item.SourceSnapshotSha256 == state.Content.SourceSnapshotSha256 &&
        ValidStateActor(item.CompletedByMemberId, item.CompletedByActor,
            item.CompletedByDisplay, item.CompletedAt) &&
        (item.Outcome == "built"
            ? item.ArchiveArtifactId is { } archiveId && archiveId != Uuid.Empty && Sha256(item.ArchiveSha256) &&
              item.ManifestId is { } manifestId && manifestId != Uuid.Empty && item.FailureCode is null &&
              item.IncludedRecordCount == state.Content.Records.Count(record => record.Disposition == "included") &&
              item.WithheldRecordCount == state.Content.Records.Count(record => record.Disposition == "withheld")
            : item.ArchiveArtifactId is null && item.ArchiveSha256 is null && item.ManifestId is null &&
              item.FailureCode is { } failureCode && Token(failureCode, 100));

    static bool ValidAttestDelivery(ClientExportAttestDeliveryView item, ClientExportContent content) =>
        content.RequiredAttestPackages.Any(package => package.EngagementId == item.EngagementId &&
            package.PackageId == item.PackageId && package.ManifestSha256 == item.ManifestSha256) &&
        item.Decision is "delivered" or "not_delivered" && Bounded(item.Rationale, 1, MaximumTextLength) &&
        ValidActor(item.RecordedByMemberId, item.RecordedByDisplay, item.RecordedAt) &&
        (item.Decision != "delivered" || Bounded(item.Recipient, 1, 300) && Bounded(item.DeliveryReference, 1, 500));

    static bool ValidAttestDeliveryHistory(IReadOnlyList<ClientExportAttestDeliveryView> deliveries) =>
        deliveries.GroupBy(item => item.PackageId).All(history =>
        {
            var attempts = history.ToArray();
            return attempts.Length <= MaximumAttestDeliveryAttempts &&
                attempts.Zip(attempts.Skip(1)).All(pair => pair.First.Decision != "delivered" &&
                    pair.Second.RecordedAt >= pair.First.RecordedAt);
        });

    static bool HasLatestSuccessfulAttestDelivery(IReadOnlyList<ClientExportAttestDeliveryView> deliveries,
        ClientExportAttestPackageContent package)
    {
        var latest = deliveries.LastOrDefault(item => item.PackageId == package.PackageId);
        return latest is { Decision: "delivered" } && latest.EngagementId == package.EngagementId &&
            latest.ManifestSha256 == package.ManifestSha256;
    }

    static bool ValidDelivery(ClientExportDeliveryView item, ClientExportView state)
    {
        if (!Bounded(item.Recipient, 1, 300) || !Bounded(item.DeliveryReference, 1, 500) ||
            state.BuildAttempts.Count == 0)
            return false;
        var attempt = state.BuildAttempts[^1];
        return attempt.Outcome == "built" && attempt.ArchiveArtifactId == item.ArchiveArtifactId &&
            attempt.ArchiveSha256 == item.ArchiveSha256 && ValidActor(item.RecordedByMemberId,
                item.RecordedByDisplay, item.DeliveredAt) &&
            state.Content.RequiredAttestPackages.All(package =>
                HasLatestSuccessfulAttestDelivery(state.AttestPackageDeliveries, package));
    }

    static bool SameRequest(ClientExportView state, ClientExportContent? content,
        Uuid actor, string display, DateTimeOffset at) => NormalizeContent(content) is { } normalized &&
        Digest(normalized) == state.ContentSha256 && state.RequestedByMemberId == actor &&
        state.RequestedByDisplay == Clean(display) && state.RequestedAt == at;

    static bool SameBuildAttempt(ClientExportBuildAttemptView prior,
        ClientExportBuildResultContent result, ActorReference actor, DateTimeOffset at) =>
        prior.AttemptNumber == result.AttemptNumber && prior.Outcome == result.Outcome &&
        prior.ArchiveArtifactId == result.ArchiveArtifactId && prior.ArchiveSha256 == result.ArchiveSha256 &&
        prior.FailureCode == result.FailureCode && prior.ManifestId == result.ManifestId &&
        prior.IncludedRecordCount == result.IncludedRecordCount && prior.WithheldRecordCount == result.WithheldRecordCount &&
        prior.SourceSnapshotId == result.SourceSnapshotId && prior.SourceSnapshotSha256 == NormalizeHash(result.SourceSnapshotSha256) &&
        prior.EffectiveCompletedBy == actor && prior.CompletedAt == at;

    static bool ValidInitialState(ClientExportView state) => state.Status == "requested" && state.Revision == 1 &&
        state.BuildAttempts.Count == 0 && state.AttestPackageDeliveries.Count == 0 && state.Delivery is null && !state.IsComplete;

    static bool ValidStatus(ClientExportView state)
    {
        var status = state.Delivery is not null ? "delivered" : state.BuildAttempts.Count == 0 ? "requested" :
            state.BuildAttempts[^1].Outcome == "built" ? "built" : "build_failed";
        return state.Status == status;
    }

    static ClientExportView Freeze(ClientExportView state) => state with
    {
        Content = state.Content with
        {
            Records = Freeze(state.Content.Records.Select(item => item with
            {
                ArtifactIds = Freeze(item.ArtifactIds.ToArray())
            }).ToArray()),
            RequiredAttestPackages = Freeze(state.Content.RequiredAttestPackages.ToArray()),
        },
        BuildAttempts = Freeze(state.BuildAttempts.ToArray()),
        AttestPackageDeliveries = Freeze(state.AttestPackageDeliveries.ToArray()),
    };

    static bool Prefix<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) => prior.Count <= next.Count &&
        prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));
    static bool ValidIds(IReadOnlyList<Uuid> ids) => ids.All(item => item != Uuid.Empty) && ids.Distinct().Count() == ids.Count;
    static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
    static string IdKey(Uuid id) => id.ToString();
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    static bool Bounded(string? value, int min, int max) => value is not null && value.Trim().Length >= min && value.Trim().Length <= max;
    static bool Token(string? value, int max) => Bounded(value, 1, max) && value!.Trim().All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty && Bounded(display, 1, 200) && at != default;
    static bool ValidStateActor(Uuid? memberId, ActorReference? actor, string? display,
        DateTimeOffset at)
    {
        if (at == default || !Bounded(display, 1, 200))
            return false;
        if (memberId is { } id)
            return ValidActor(id, display, at) &&
                (actor is null || actor == ActorReference.ForMember(id, Clean(display)));
        return actor is { Kind: "system_process", Id: { } processId, Display: { } actorDisplay } &&
            processId.StartsWith("reactor:", StringComparison.Ordinal) &&
            actorDisplay == Clean(display) && Bounded(processId, 1, 200);
    }
    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("client export", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T item) => Freeze(values.Append(item).ToArray());
}
