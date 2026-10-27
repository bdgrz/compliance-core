using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

/// <summary>
///     One attributable proposal of a mapping. <c>MappingId</c> is the id of this proposal; review and retirement
///     name it. Status is proposed, accepted, rejected, superseded, or retired; only an accepted version counts
///     toward coverage.
/// </summary>
public sealed record ControlCriterionMappingVersionView(int VersionNumber, Uuid MappingId,
    Uuid ControlVersionId, string Status, string Rationale, string ApplicabilityExplanation,
    ActorReference ProposedBy, DateTimeOffset ProposedAt, Uuid? ReviewDecisionId,
    ActorReference? ReviewedBy, string? ReviewRationale, DateTimeOffset? ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId, ActorReference? RetiredBy, string? RetirementRationale,
    DateTimeOffset? RetiredAt);
