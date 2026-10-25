using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Frozen descriptive source identity and authority selectors; eligibility is always evaluated from current owners.</summary>
public sealed record WorkAssignmentSourceIdentity(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, string Kind,
    Uuid SourceId, Uuid? ControlId, Uuid? FindingId, string Summary, string Reason, DateOnly? DueOn,
    string? Materiality, string NextAction, string ActionPath, OperatingHolder Responsible, OperatingHolder? Backup,
    IReadOnlyList<Uuid> Excluded, DateTimeOffset CreatedAt, Uuid? RestrictedSystemInstanceId,
    bool RequiresSeparationOfDutiesWaiver, Uuid? RequiredManagementProgramId, Uuid? AcknowledgementPersonId, Uuid? AccessReviewCampaignId)
{
    public static WorkAssignmentSourceIdentity Capture(Uuid tenantId, Uuid programId, WorkCandidate candidate) =>
        new(tenantId, programId, candidate.WorkItemId, candidate.Kind, candidate.SourceId, candidate.ControlId,
            candidate.FindingId, candidate.Summary, candidate.Reason, candidate.DueOn, candidate.Materiality,
            candidate.NextAction, candidate.ActionPath, candidate.Responsible, candidate.Backup,
            candidate.Excluded.OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray(), candidate.CreatedAt,
            candidate.RestrictedSystemInstanceId, candidate.RequiresSeparationOfDutiesWaiver,
            candidate.RequiredManagementProgramId, candidate.AcknowledgementPerson?.PersonId, candidate.AccessReviewCampaignId);

    public WorkCandidate Describe() => new(WorkItemId, Kind, SourceId, ControlId, FindingId, Summary, Reason, DueOn,
        Materiality, NextAction, ActionPath, Responsible, Backup, Excluded.ToHashSet(), CreatedAt)
    {
        ProgramId = ProgramId,
        AccessReviewCampaignId = AccessReviewCampaignId,
        RestrictedSystemInstanceId = RestrictedSystemInstanceId,
        RequiresSeparationOfDutiesWaiver = RequiresSeparationOfDutiesWaiver,
        RequiredManagementProgramId = RequiredManagementProgramId,
    };
}
