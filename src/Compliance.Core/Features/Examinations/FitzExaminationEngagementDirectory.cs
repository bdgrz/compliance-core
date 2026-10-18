using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class FitzExaminationEngagementDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/examination-engagement-directory/projection-v2",
        ProjectorName), IExaminationEngagementDirectoryReader, IExaminationEngagementDirectoryProjection
{
    public const string ProjectorName = "ExaminationEngagementDirectoryV2";

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, engagement, baseline) = domainEvent switch
        {
            ExaminationEngagementOpened opened => (opened.TenantId, opened.Engagement, null),
            ExaminationEngagementRevised revised => (revised.TenantId, revised.Engagement, null),
            TypeIBaselineFrozen frozen => (frozen.TenantId, frozen.Engagement, frozen.Baseline),
            TypeIBaselineAmended amended => (amended.TenantId, amended.Engagement, amended.Baseline),
            _ => (Uuid.Empty, null, null),
        };
        if (engagement is null)
        {
            if (domainEvent is ExaminationEngagementOpened or ExaminationEngagementRevised or
                TypeIBaselineFrozen or TypeIBaselineAmended)
                throw new InvalidOperationException("The examination engagement projection event is invalid.");
            return;
        }
        if ((domainEvent is TypeIBaselineFrozen or TypeIBaselineAmended) && baseline is null)
            throw new InvalidOperationException("The Type I baseline projection event is invalid.");
        if (tenantId == Uuid.Empty || engagement.TenantId != tenantId ||
            engagement.ProgramId == Uuid.Empty || engagement.ExaminationEngagementId == Uuid.Empty ||
            engagement.Revision <= 0 || engagement.UpdatedAt == default)
            throw new InvalidOperationException("The examination engagement projection event is invalid.");

        if (baseline is not null)
            await ApplyBaselineAsync(domainEvent, tenantId, engagement, baseline, ct)
                .ConfigureAwait(false);

        var current = await ExaminationEngagementDirectorySchema.Directory.GetAsync(Transaction,
            engagement.ExaminationEngagementId, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (domainEvent is not ExaminationEngagementOpened || engagement.Revision != 1)
                throw new InvalidOperationException(
                    "An examination engagement must be opened before later revisions are projected.");
            await ExaminationEngagementDirectorySchema.Directory.InsertAsync(Transaction,
                engagement, ct).ConfigureAwait(false);
            return;
        }
        if (current.TenantId != tenantId || current.ProgramId != engagement.ProgramId ||
            current.ExaminationEngagementId != engagement.ExaminationEngagementId)
            throw new InvalidOperationException("The examination engagement identity changed during projection.");
        if (engagement.Revision < current.Revision)
            return;
        if (engagement.Revision == current.Revision)
        {
            if (!SameRevision(engagement, current))
                throw new InvalidOperationException("An examination engagement revision has conflicting content.");
            return;
        }
        if (engagement.Revision != current.Revision + 1)
            throw new InvalidOperationException("The examination engagement projection skipped a revision.");

        await ExaminationEngagementDirectorySchema.Directory.ReplaceAsync(Transaction, current,
            engagement, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyBaselineAsync(DomainEvent domainEvent, Uuid tenantId,
        ExaminationEngagementView engagement, TypeIBaselineView baseline, CancellationToken ct)
    {
        var occurredAt = domainEvent switch
        {
            TypeIBaselineFrozen frozen => frozen.OccurredAt,
            TypeIBaselineAmended amended => amended.OccurredAt,
            _ => throw new InvalidOperationException("The baseline projection event is invalid."),
        };
        if (baseline.TenantId != tenantId || baseline.ProgramId != engagement.ProgramId ||
            baseline.ExaminationEngagementId != engagement.ExaminationEngagementId ||
            baseline.TypeIBaselineId == Uuid.Empty || baseline.Revision <= 0 ||
            baseline.UpdatedAt != occurredAt || engagement.UpdatedAt != occurredAt ||
            baseline.ActorMemberId == Uuid.Empty || baseline.ActorMemberId != engagement.ActorMemberId ||
            string.IsNullOrWhiteSpace(baseline.ActorDisplay) ||
            !string.Equals(baseline.ActorDisplay, engagement.ActorDisplay, StringComparison.Ordinal) ||
            baseline.ChangedRecordIds is null || baseline.AffectedPackageMaterialIds is null ||
            !MatchesManifest(baseline))
            throw new InvalidOperationException("The Type I baseline projection event is invalid.");

        TypeIBaselineView? predecessor = null;
        switch (domainEvent)
        {
            case TypeIBaselineFrozen:
                if (baseline.Status != "frozen" || baseline.Revision != 1 ||
                    baseline.AmendsBaselineId is not null || baseline.AmendmentReason is not null ||
                    baseline.AmendmentDepth != 0 || baseline.ChangedRecordIds.Count != 0 ||
                    baseline.AffectedPackageMaterialIds.Count != 0 ||
                    string.IsNullOrWhiteSpace(baseline.ConfirmationRationale) ||
                    baseline.ConfirmationRationale.Length > 2000)
                    throw new InvalidOperationException("The Type I baseline freeze event is invalid.");
                break;
            case TypeIBaselineAmended:
                if (baseline.Status != "amended" || baseline.AmendsBaselineId is not { } predecessorId ||
                    predecessorId == Uuid.Empty || predecessorId == baseline.TypeIBaselineId ||
                    string.IsNullOrWhiteSpace(baseline.AmendmentReason) ||
                    baseline.AmendmentReason.Length > 2000 || baseline.AmendmentDepth is <= 0 or > 8 ||
                    baseline.ChangedRecordIds.Count is 0 or > 256 ||
                    baseline.ChangedRecordIds.Any(static id => id == Uuid.Empty) ||
                    baseline.ChangedRecordIds.Distinct().Count() != baseline.ChangedRecordIds.Count ||
                    baseline.AffectedPackageMaterialIds.Any(static id => id == Uuid.Empty) ||
                    baseline.AffectedPackageMaterialIds.Distinct().Count() !=
                    baseline.AffectedPackageMaterialIds.Count)
                    throw new InvalidOperationException("The Type I baseline amendment event is invalid.");
                predecessor = await ExaminationEngagementDirectorySchema.Baselines.GetAsync(Transaction,
                    predecessorId, ct).ConfigureAwait(false);
                if (predecessor is null || predecessor.TenantId != tenantId ||
                    predecessor.ProgramId != baseline.ProgramId ||
                    predecessor.ExaminationEngagementId != baseline.ExaminationEngagementId ||
                    predecessor.Status is not ("frozen" or "amended") ||
                    predecessor.Revision + 1 != baseline.Revision ||
                    predecessor.AmendmentDepth + 1 != baseline.AmendmentDepth)
                    throw new InvalidOperationException(
                        "A Type I baseline amendment must link to its existing predecessor.");
                break;
        }

        var existing = await ExaminationEngagementDirectorySchema.Baselines.GetAsync(Transaction,
            baseline.TypeIBaselineId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!SameBaseline(existing, baseline))
                throw new InvalidOperationException("A Type I baseline identity has conflicting content.");
            return;
        }

        await ExaminationEngagementDirectorySchema.Baselines.InsertAsync(Transaction, baseline, ct)
            .ConfigureAwait(false);
    }

    static bool MatchesManifest(TypeIBaselineView baseline)
    {
        if (baseline.Sources is null || baseline.ScopeDifferences is null ||
            baseline.Sources.Count is 0 or > 256 || baseline.ScopeDifferences.Count > 1000 ||
            baseline.ScopeDifferences.Any(static value => string.IsNullOrWhiteSpace(value) ||
                value.Length > 1000))
            return false;
        try
        {
            var identities = new HashSet<(string Kind, Uuid RecordId)>();
            foreach (var source in baseline.Sources)
                if (source is null || !ValidKind(source.Kind) || source.RecordId == Uuid.Empty ||
                    source.VersionId == Uuid.Empty || source.Revision <= 0 ||
                    !ValidDigest(source.ContentSha256) ||
                    !identities.Add((source.Kind, source.RecordId)))
                    return false;

            var preview = TypeIBaselinePreview.Create(baseline.TenantId, baseline.ProgramId,
                baseline.ExaminationEngagementId, baseline.Sources, [], baseline.ScopeDifferences);
            return preview.CanonicalManifest == baseline.CanonicalManifest &&
                   preview.ContentSha256 == baseline.ContentSha256 &&
                   ValidDigest(baseline.ContentSha256);
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
        {
            return false;
        }
    }

    static bool ValidKind(string? value) => value is { Length: > 0 and <= 64 } &&
        value[0] is >= 'a' and <= 'z' && value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool SameBaseline(TypeIBaselineView left, TypeIBaselineView right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.ExaminationEngagementId == right.ExaminationEngagementId &&
        left.TypeIBaselineId == right.TypeIBaselineId && left.Status == right.Status &&
        left.Revision == right.Revision && left.Sources.SequenceEqual(right.Sources) &&
        left.ScopeDifferences.SequenceEqual(right.ScopeDifferences, StringComparer.Ordinal) &&
        left.CanonicalManifest == right.CanonicalManifest && left.ContentSha256 == right.ContentSha256 &&
        left.UpdatedAt == right.UpdatedAt && left.ActorMemberId == right.ActorMemberId &&
        left.ActorDisplay == right.ActorDisplay &&
        left.ConfirmationRationale == right.ConfirmationRationale &&
        left.AmendsBaselineId == right.AmendsBaselineId && left.AmendmentReason == right.AmendmentReason &&
        left.ChangedRecordIds.SequenceEqual(right.ChangedRecordIds) &&
        left.AffectedPackageMaterialIds.SequenceEqual(right.AffectedPackageMaterialIds) &&
        left.AmendmentDepth == right.AmendmentDepth;

    static bool SameRevision(ExaminationEngagementView left, ExaminationEngagementView right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.ExaminationEngagementId == right.ExaminationEngagementId &&
        left.EngagementType == right.EngagementType && left.AuditFirm == right.AuditFirm &&
        left.PeriodStart == right.PeriodStart && left.PeriodEnd == right.PeriodEnd &&
        left.TrustServicesCategories.SequenceEqual(right.TrustServicesCategories,
            StringComparer.Ordinal) && left.BoundaryId == right.BoundaryId &&
        left.ManagementContactMemberIds.SequenceEqual(right.ManagementContactMemberIds) &&
        left.Status == right.Status && left.Revision == right.Revision &&
        left.UpdatedAt == right.UpdatedAt && left.ActorMemberId == right.ActorMemberId &&
        left.ActorDisplay == right.ActorDisplay;

    public async ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId,
        Uuid examinationEngagementId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ExaminationEngagementDirectorySchema.Directory.GetAsync(tx,
            examinationEngagementId, ct).ConfigureAwait(false);
    }

    public async ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId,
        Uuid typeIBaselineId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ExaminationEngagementDirectorySchema.Baselines.GetAsync(tx,
            typeIBaselineId, ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString())), ct);
}
