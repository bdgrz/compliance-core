using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

/// <summary>
///     A program's reviewed decision that one criterion of an edition does not apply, keyed by
///     (edition_id, criterion_identifier). <c>DecisionId</c> is the id of the line's first proposal; each version
///     carries the id of its own proposal, and <c>Revision</c> counts the facts of the line. Status is pending,
///     not_applicable, rejected, or withdrawn. A not-applicable decision excludes the criterion from coverage; it never
///     states that the criterion is satisfied.
/// </summary>
public sealed record CriterionApplicabilityView(Uuid TenantId, Uuid ProgramId, Uuid DecisionId,
    Uuid EditionId, string CriterionIdentifier, long Revision, string Status,
    int? ActiveVersionNumber, IReadOnlyList<CriterionApplicabilityVersionView> Versions);
