using System.Text.Json.Serialization;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>A deterministic expected control performance derived from the approved plan.</summary>
public sealed record TypeIIPeriodOccurrenceView(
    Uuid OccurrenceId,
    Uuid ControlId,
    Uuid ControlVersionId,
    int Sequence,
    DateOnly? DueOn,
    Uuid OwnerMemberId,
    string Cadence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? ExpectedEvidenceDescriptions = null);
