using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Models;

/// <summary>
///     The mapping line of one control and one immutable catalog entry, keyed by
///     (control, edition_id, criterion_identifier). <c>MappingId</c> is the id of the line's first proposal; each
///     version carries the id of its own proposal. <c>Revision</c> counts the facts of this line.
///     Status is pending, active, rejected, or retired.
///     A mapping records a claimed relationship; it never asserts that a criterion is satisfied.
/// </summary>
public sealed record ControlCriterionMappingView(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, Uuid ControlId, Uuid EditionId, string CriterionIdentifier,
    string CriterionKind, long Revision, string Status, int? ActiveVersionNumber,
    Uuid? ActiveControlVersionId, IReadOnlyList<ControlCriterionMappingVersionView> Versions);
