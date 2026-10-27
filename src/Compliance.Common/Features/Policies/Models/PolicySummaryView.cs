using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

/// <summary>
///     A policy row in the program inventory. Retired policies remain listed with a retired status;
///     immutable version history is returned by the policy versions read.
/// </summary>
public sealed record PolicySummaryView(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, string Title, string Status, string? PendingStatus, long Revision,
    long? CurrentVersion, DateOnly? CurrentEffectiveFrom, DateOnly? NextReviewDueOn,
    bool ReviewOverdue, DateTimeOffset LastChangedAt);
