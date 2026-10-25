using Cntryl.Portia;
using Bdgrz.Compliance.Features.SystemDescription;

namespace Bdgrz.Compliance.Features.Changes;

sealed record CorrelatedSystemDescriptionDecision(string Decision, Uuid DecisionId,
    string Rationale, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset DecidedAt);

static class SystemDescriptionDecisionCorrelation
{
    public static Uuid NoChangeDecisionId(Uuid versionId, Uuid changeId) =>
        Uuid.CreateVersion5(versionId, "no-change:" + changeId);

    public static CorrelatedSystemDescriptionDecision? Find(SystemDescriptionReadSnapshot description,
        Uuid tenantId, Uuid programId, Uuid targetId, Uuid versionId, Uuid decisionId,
        string decision, string? assessmentSha256 = null)
    {
        var view = description.Read();
        if (view.TenantId != tenantId || view.ProgramId != programId ||
            view.SystemDescriptionId != description.Id || targetId == Uuid.Empty ||
            versionId == Uuid.Empty)
            return null;

        var version = description.FindVersion(versionId);
        if (version is null || version.TenantId != tenantId || version.ProgramId != programId ||
            version.SystemDescriptionId != description.Id)
            return null;

        if (decision == "approved_successor" && decisionId == versionId &&
            version.Status == "approved" && version.Content.SignificantChangeIds.Contains(targetId) &&
            description.FindApproval(versionId) is { } approval &&
            approval.Version == version.Version && approval.VersionId == versionId)
            return new CorrelatedSystemDescriptionDecision(decision, versionId,
                approval.Rationale, approval.ApproverMemberId, approval.ApproverDisplay,
                approval.ApprovedAt);

        if (decision == "no_change" && decisionId == NoChangeDecisionId(versionId, targetId) &&
            version.Content.PeriodChanges.FirstOrDefault(change => change.ChangeId == targetId)
                is { IsMaterial: false, AssessmentSha256: { } recordedAssessmentSha256 } &&
            string.Equals(recordedAssessmentSha256, assessmentSha256, StringComparison.Ordinal) &&
            description.NoChangeDecisions.FirstOrDefault(item => item.VersionId == versionId &&
                item.ChangeId == targetId) is { } noChange && noChange.Version == version.Version)
            return new CorrelatedSystemDescriptionDecision(decision, decisionId,
                noChange.Rationale, noChange.DecidedByMemberId, noChange.DecidedByDisplay,
                noChange.DecidedAt);

        return null;
    }
}
