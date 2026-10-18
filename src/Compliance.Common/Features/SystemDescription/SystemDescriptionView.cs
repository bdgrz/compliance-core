using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Current narrative and its immutable versions, decisions, bindings, and exports.</summary>
public sealed record SystemDescriptionView(Uuid TenantId, Uuid ProgramId,
    Uuid SystemDescriptionId, long Revision, SystemDescriptionVersionView? CurrentVersion,
    IReadOnlyList<SystemDescriptionVersionView> Versions,
    IReadOnlyList<SystemDescriptionReviewView> Reviews,
    IReadOnlyList<SystemDescriptionApprovalView> Approvals,
    IReadOnlyList<SystemDescriptionNoChangeDecisionView> NoChangeDecisions,
    IReadOnlyList<SystemDescriptionBindingView> Bindings,
    IReadOnlyList<SystemDescriptionExportView> Exports);
