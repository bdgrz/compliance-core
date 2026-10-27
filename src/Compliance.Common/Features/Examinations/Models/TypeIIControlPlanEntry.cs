using System.Text.Json.Serialization;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>An exact control version, accountable owner, cadence, and expected occurrence count.</summary>
public sealed record TypeIIControlPlanEntry(
    Uuid ControlId,
    Uuid ControlVersionId,
    string ControlTitle,
    Uuid? OwnerMemberId,
    string Cadence,
    int ExpectedOccurrences,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveThrough,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DueWithinDays = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? ExpectedEvidenceDescriptions = null);
