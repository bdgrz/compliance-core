using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Verified preparation data consumed synchronously by the native owner at its exact cut.</summary>
public sealed record FrozenComplianceIncidentMutation(ComplianceIncidentView? Before, ComplianceIncidentView After,
    ComplianceIncidentCurrentAuthority? BeforeAuthority, ComplianceIncidentCurrentAuthority Authority,
    ImmutableContentRef CurrentContent, ImmutableContentRef Change, ComplianceIncidentMutation Mutation, object Payload,
    ComplianceIncidentAuthorityHeader Header);
