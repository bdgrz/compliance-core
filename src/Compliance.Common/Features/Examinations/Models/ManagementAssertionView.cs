using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Versioned management assertion, exact inputs, signers, and independent decisions.</summary>
public sealed record ManagementAssertionView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ManagementAssertionId,
    Uuid ExaminationId,
    string ScopeKind,
    string Status,
    long Revision,
    int CurrentVersionNumber,
    ManagementAssertionVersionView? CurrentVersion,
    IReadOnlyList<ManagementAssertionVersionView> Versions,
    IReadOnlyList<ManagementAssertionApprovalView> Approvals,
    DateTimeOffset UpdatedAt);
