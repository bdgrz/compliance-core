using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The authored content of a personal engagement decision, a closed union selected by
///     <see cref="EngagementPersonalDecisionKind" />. It is frozen at admission and never read from the command again.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling =
    JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ManagementAcknowledgementContent), "management_acknowledgement")]
[JsonDerivedType(typeof(PartnerEvaluationContent), "partner_evaluation")]
public abstract record EngagementPersonalDecisionContent
{
    private protected EngagementPersonalDecisionContent()
    {
    }
}

/// <summary>
///     A management acknowledgement. <paramref name="CompleteServiceRecordIds" /> is the complete authored identity
///     selection; <paramref name="UserId" /> is the authenticated personal subject.
/// </summary>
public sealed record ManagementAcknowledgementContent(ImmutableSelectionManifest CompleteServiceRecordIds,
    string Statement, Uuid UserId) : EngagementPersonalDecisionContent;

/// <summary>A partner evaluation bound to an exact rule version, rule digest and service history digest.</summary>
public sealed record PartnerEvaluationContent(long RuleVersion, string ExpectedRuleContentDigest,
    string ExpectedServiceHistoryDigest, string Rationale) : EngagementPersonalDecisionContent;
