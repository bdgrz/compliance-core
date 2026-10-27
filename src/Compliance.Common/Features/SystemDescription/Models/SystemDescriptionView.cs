using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Current narrative and its immutable versions, decisions, bindings, and snapshot bindings.</summary>
public sealed record SystemDescriptionView(Uuid TenantId, Uuid ProgramId,
    Uuid SystemDescriptionId, long Revision, SystemDescriptionVersionView? CurrentVersion,
    IReadOnlyList<SystemDescriptionVersionView> Versions,
    IReadOnlyList<SystemDescriptionReviewView> Reviews,
    IReadOnlyList<SystemDescriptionApprovalView> Approvals,
    IReadOnlyList<SystemDescriptionNoChangeDecisionView> NoChangeDecisions,
    IReadOnlyList<SystemDescriptionBindingView> Bindings);
