using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     An accountable work item linked to its exact source: control_occurrence,
///     occurrence_review, or corrective_action. Completing it happens in the source workflow.
/// </summary>
public sealed record WorkItemView(string Kind, Uuid SourceId, Uuid? ControlId, Uuid? FindingId,
    string Summary, DateOnly? DueOn, bool Overdue, string? Materiality);
