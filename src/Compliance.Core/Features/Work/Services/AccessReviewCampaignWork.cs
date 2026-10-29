using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Work.Services;

static class AccessReviewCampaignWork
{
    public static IReadOnlyCollection<string> ProjectedKinds =>
        [WorkSource.AccessReviewReview, WorkSource.AccessReviewRemediation];

    public static bool IsReview(string kind) => kind == WorkSource.AccessReviewReview;

    public static bool IsRemediation(string kind) => kind == WorkSource.AccessReviewRemediation;

    public static IReadOnlyList<WorkCandidate> Candidates(AccessReviewCampaignWorkItemState state,
        DateTimeOffset now, Uuid? workItemId = null)
    {
        if (state.ProgramId == Uuid.Empty || state.IsCompleted ||
            state.SystemInstanceId == Uuid.Empty)
            return [];

        if (state.Decision is null)
        {
            if (state.CurrentReviewerMemberId is not { } reviewerMemberId ||
                reviewerMemberId == Uuid.Empty)
                return [];

            var candidate = CreateCandidate(state, reviewerMemberId,
                WorkSource.AccessReviewReview,
                    "record_decision", "decisions",
                    "Review a frozen access assignment",
                    "This frozen campaign item still needs a reviewer decision.");
            return workItemId is null || candidate.WorkItemId == workItemId
                ? [candidate]
                : [];
        }

        if (!AccessReviewVocabulary.RequiresRemediation(state.Decision) ||
            state.IsVerified ||
            state.HasException &&
            (state.ExceptionExpiresAt is null || now < state.ExceptionExpiresAt) ||
            state.CurrentRemediationOwnerMemberId is not { } ownerMemberId ||
            ownerMemberId == Uuid.Empty)
            return [];

        var remediationCandidate = state.HasProviderChange
            ? CreateCandidate(state, ownerMemberId, WorkSource.AccessReviewRemediation,
                "verify_remediation", "remediation-verifications",
                "Verify a provider-side remediation",
                "A provider change is recorded; accepted-population verification is still required.")
            : CreateCandidate(state, ownerMemberId, WorkSource.AccessReviewRemediation,
                "record_remediation_change", "remediation-changes",
                "Remediate a frozen access assignment",
                "The review decision requires provider-side remediation.");
        return workItemId is null || remediationCandidate.WorkItemId == workItemId
            ? [remediationCandidate]
            : [];
    }

    static WorkCandidate CreateCandidate(AccessReviewCampaignWorkItemState state,
        Uuid responsibleMemberId, string kind, string nextAction,
        string endpoint, string summary, string reason)
    {
        HashSet<Uuid> excluded = [];
        if (IsReview(kind) && state.SubjectMemberId is { } subjectMemberId)
            excluded.Add(subjectMemberId);
        var requiresWaiver = IsReview(kind) && excluded.Contains(responsibleMemberId);
        var workItemId = WorkItemIdFor(state.TenantId, state.ProgramId, state.CampaignId,
            state.ItemId, kind);
        return new WorkCandidate(workItemId, kind, state.ItemId, null, null, summary, reason,
            state.DueOn, state.Privileged ? "high" : null, nextAction,
            $"/api/v1/tenants/{state.TenantId}/access-review-campaigns/{state.CampaignId}/items/{state.ItemId}/{endpoint}",
            new OperatingHolder(OperatingAuthority.MemberHolder, responsibleMemberId), null,
            excluded, state.LaunchedAt)
        {
            ProgramId = state.ProgramId,
            AccessReviewCampaignId = state.CampaignId,
            RestrictedSystemInstanceId = state.SystemInstanceId,
            RequiresSeparationOfDutiesWaiver = requiresWaiver,
        };
    }

    public static Uuid WorkItemIdFor(Uuid tenantId, Uuid programId, Uuid campaignId,
        Uuid itemId, string kind) => Uuid.CreateVersion5(tenantId,
        $"{programId}\n{campaignId}\n{itemId}\n{kind}\nv1");
}
