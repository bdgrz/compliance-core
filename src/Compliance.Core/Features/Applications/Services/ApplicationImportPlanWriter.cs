using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Services;

/// <summary>
///     Freezes the complete plan of a batch as immutable content: the rows, every canonical target group (each also
///     as its own content, so a receiver carries exactly the intent of its group), the source claims it was frozen
///     against, and the intent that references them all. Group content ids derive from the plan's content id, so a
///     receiver that rewrites a group reads the same reference.
/// </summary>
static class ApplicationImportPlanWriter
{
    public static Uuid GroupContentId(Uuid planContentId, Uuid applicationId) =>
        Uuid.CreateVersion5(planContentId, $"import-target-group:{applicationId}");

    /// <summary>The reference of the stand-alone content of one group of a sealed plan.</summary>
    public static Task<ImmutableContentRef> GroupRefAsync(IImmutableContentStore content, Uuid tenantId,
        ImmutableContentRef plan, ImportTargetGroupIntent group, CancellationToken ct) =>
        content.WriteJsonAsync(ApplicationImportContent.Realm(tenantId), GroupContentId(plan.ContentId, group.ApplicationId),
            ImportTargetGroupIntent.Schema, group, ct);

    public static async Task<ImportTargetGroupIntent> FreezeGroupAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid batchId, ApplicationImportTargetDraft draft, CancellationToken ct)
    {
        var rows = await ApplicationImportContent.WriteSelectionAsync(content, tenantId, "import-planned-row.v1",
            draft.Rows, SelectionOrder.Authored, ct).ConfigureAwait(false);
        var candidate = await ApplicationImportContent.WriteAsync(content, tenantId,
            ApplicationImportCandidateContent.Schema, draft.Candidate, ct).ConfigureAwait(false);
        var proofs = await ApplicationImportContent.WriteSelectionAsync(content, tenantId,
            ApplicationImportRetirementProof.ItemSchema, draft.RetirementProofs, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        return new ImportTargetGroupIntent(batchId, draft.ApplicationId, draft.InitialRevision, rows, candidate,
            draft.Candidate.Revision, proofs);
    }

    /// <summary>Writes every manifest and then the plan itself under a content id chosen up front.</summary>
    public static async Task<(ImmutableContentRef Plan, ImportFrozenPlanIntent Intent,
        IReadOnlyList<ImportTargetGroupIntent> Groups)> FreezeAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid batchId, DurableIntentHeader header, ApplicationImportBatch batch, ApplicationImportSourceView source,
        IReadOnlyList<ApplicationImportPlannedRow> rows, IReadOnlyList<ApplicationImportTargetDraft> drafts,
        Uuid approverMemberId, string approverDisplay, AuthoritativeReadCut cut, CancellationToken ct)
    {
        var groups = new List<ImportTargetGroupIntent>(drafts.Count);
        foreach (var draft in drafts)
            groups.Add(await FreezeGroupAsync(content, tenantId, batchId, draft, ct).ConfigureAwait(false));
        var rowManifest = await ApplicationImportContent.WriteSelectionAsync(content, tenantId, "import-planned-row.v1",
            rows, SelectionOrder.Authored, ct).ConfigureAwait(false);
        var groupManifest = await ApplicationImportContent.WriteSelectionAsync<ImportTargetGroupIntent>(content,
            tenantId, ImportTargetGroupIntent.Schema, groups, SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var claimManifest = await ApplicationImportContent.WriteSelectionAsync(content, tenantId,
            ApplicationImportSourceClaimRecord.ItemSchema, source.Claims, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var intent = new ImportFrozenPlanIntent(header, batchId, batch.SourceKey!, batch.SourceNamespace!,
            batch.Coverage!, batch.ContentDigest!, batch.SubmitterMemberId, approverMemberId, rowManifest,
            groupManifest, claimManifest, batch.HasCurrentProposal ? ProposalRef(batch) : null, cut,
            batch.SubmitterDisplay!, approverDisplay, source.Source.Revision);
        var planId = Uuid.CreateVersion4();
        // Group contents are rewritten under ids derived from the plan so a receiver reads the same reference.
        var planRef = await content.WriteJsonAsync(ApplicationImportContent.Realm(tenantId), planId,
            ImportFrozenPlanIntent.Schema, intent, ct).ConfigureAwait(false);
        foreach (var group in groups)
            _ = await GroupRefAsync(content, tenantId, planRef, group, ct).ConfigureAwait(false);
        return (planRef, intent, groups.AsReadOnly());
    }

    static ImmutableContentRef? ProposalRef(ApplicationImportBatch batch) => batch.Proposal?.Proposal;
}
