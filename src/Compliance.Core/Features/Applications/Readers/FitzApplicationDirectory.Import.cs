using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Readers;

sealed partial class FitzApplicationDirectory
{
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginApplicationBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginApplicationBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginApplicationBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An application projection batch requires its tenant realm.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyCommittedImportAsync(ApplicationImportCommitted marker, ImportFrozenPlanIntent plan,
        IReadOnlyList<ApplicationImportPlannedRow> rows, IReadOnlyList<ApplicationImportEffectProof> proofs,
        CancellationToken ct = default)
    {
        if (marker.TenantId != _batchTenantId || marker.BatchId != plan.BatchId ||
            marker.SourceKey != plan.SourceKey || marker.SourceNamespace != plan.SourceNamespace ||
            marker.PlanSha256 != marker.Intent.Sha256 || marker.FrozenRows != plan.Rows.Content ||
            proofs.Count != rows.Count || plan.Rows.Count != rows.Count ||
            rows.Any(row => proofs.Count(proof => proof.RowId == row.RowId &&
                proof.ApplicationId == row.ApplicationId) != 1))
            throw new InvalidOperationException("The import projection requires its complete committed frozen plan.");
        foreach (var row in rows)
        {
            if (row.Decision == "link_existing")
            {
                var linked = await RequireApplicationAsync(row.ApplicationId, ct).ConfigureAwait(false);
                if (linked.TenantId != marker.TenantId || linked.Revision != row.ExpectedApplicationRevision ||
                    linked.Lifecycle != "active")
                    throw new InvalidOperationException("The linked import target differs from its frozen governed revision.");
                continue;
            }
            if (row.Decision == "retire")
            {
                var retiring = await RequireApplicationAsync(row.ApplicationId, ct).ConfigureAwait(false);
                if (retiring.TenantId != marker.TenantId || retiring.Revision != row.ExpectedApplicationRevision ||
                    retiring.Lifecycle != "active" || string.IsNullOrWhiteSpace(row.RetirementReason))
                    throw new InvalidOperationException("The retirement import target differs from its frozen governed revision.");
                var retired = retiring with
                {
                    Revision = checked(retiring.Revision + 1),
                    LastChangedByMemberId = plan.ApproverMemberId,
                    LastChangedByDisplay = plan.ApproverDisplay,
                    LastChangedAt = marker.CommittedAt,
                    LastChangedBy = ActorReference.ForMember(plan.ApproverMemberId,
                        plan.ApproverDisplay),
                    Lifecycle = "retired",
                    Retirement = new RetirementView(marker.CommittedAt, row.RetirementReason, null),
                };
                await ApplicationDirectorySchema.Applications.ReplaceAsync(Transaction, retiring,
                    retired, ct).ConfigureAwait(false);
                await InsertRevisionAsync(retired, "retired", null, ct).ConfigureAwait(false);
                continue;
            }
            if (row.Decision != "create_new")
                throw new InvalidOperationException("The import projection has an unsupported row decision.");
            var view = new ApplicationView(marker.TenantId, row.ApplicationId, 1, row.Name.Trim(),
                row.Purpose.Trim(), NormalizeImportOwner(row.OwnerReference), "import", row.SourceRecordId,
                false, Gaps(row.OwnerReference, null, false, null, null), plan.ApproverMemberId,
                plan.ApproverDisplay, marker.CommittedAt)
            {
                LastChangedBy = ActorReference.ForMember(plan.ApproverMemberId, plan.ApproverDisplay),
            };
            await ApplicationDirectorySchema.Applications.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
            await InsertRevisionAsync(view, "imported", null, ct).ConfigureAwait(false);
        }
        var epoch = await ApplicationDirectorySchema.ImportEpoch.GetAsync(Transaction, "imports", ct).ConfigureAwait(false);
        var next = new ApplicationImportVisibilityEpoch(checked((epoch?.Revision ?? 0) + 1));
        if (epoch is null)
            await ApplicationDirectorySchema.ImportEpoch.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else
            await ApplicationDirectorySchema.ImportEpoch.ReplaceAsync(Transaction, epoch, next, ct).ConfigureAwait(false);
    }

    static string? NormalizeImportOwner(string? owner) => string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
}
