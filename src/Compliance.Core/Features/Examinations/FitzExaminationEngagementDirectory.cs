using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class FitzExaminationEngagementDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/examination-engagement-directory/projection",
        ProjectorName), IExaminationEngagementDirectoryReader, IExaminationEngagementDirectoryProjection
{
    public const string ProjectorName = "ExaminationEngagementDirectory";

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, engagement) = domainEvent switch
        {
            ExaminationEngagementOpened opened => (opened.TenantId, opened.Engagement),
            ExaminationEngagementRevised revised => (revised.TenantId, revised.Engagement),
            TypeIBaselineFrozen frozen => (frozen.TenantId, frozen.Engagement),
            TypeIBaselineAmended amended => (amended.TenantId, amended.Engagement),
            _ => (Uuid.Empty, null),
        };
        if (engagement is null)
            return;
        if (tenantId == Uuid.Empty || engagement.TenantId != tenantId ||
            engagement.ProgramId == Uuid.Empty || engagement.ExaminationEngagementId == Uuid.Empty ||
            engagement.Revision <= 0 || engagement.UpdatedAt == default)
            throw new InvalidOperationException("The examination engagement projection event is invalid.");

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

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString())), ct);
}
