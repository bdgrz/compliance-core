using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     Server-side source evidence for acceptance. This is returned by a trusted evidence reader and is never
///     accepted from a caller-facing request; the configured reader must prove current partner-duty authority,
///     current source snapshots, and applicable ratified rules for the exact tenant and engagement.
/// </summary>
public sealed record VerifiedEngagementAcceptance(Uuid TenantId, Uuid EngagementId,
    long ReviewedDraftRevision, Uuid ReviewTaskId, Uuid PartnerStaffMemberId, Uuid PartnerUserId,
    string AuthorityReference, string? PartnerEvaluationReference, Uuid ManagementAcknowledgementId,
    BoundaryVersionView? Boundary, BoundaryDecisionView? BoundaryApproval,
    IReadOnlyList<FirmStaffMemberView> CurrentStaff, FirmStaffMemberView CurrentPartner, long PartnerDutyRevision,
    DateTimeOffset PartnerAuthorityVerifiedAt, PartnerIndependenceEvaluationView? PartnerEvaluation = null);

/// <summary>Trusted server-side evidence resolved for one exact draft; never populated from request body fields.</summary>
public sealed record ServiceEngagementAcceptanceEvidence(VerifiedEngagementAcceptance Proof,
    IndependenceRuleVersionView RatifiedRules);
