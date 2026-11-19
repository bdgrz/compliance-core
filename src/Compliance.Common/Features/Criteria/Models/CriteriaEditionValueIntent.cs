using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One bounded field or disclosure note, set for one edition draft.</summary>
public sealed record CriteriaEditionValueIntent(Uuid EditionId, Uuid DraftGeneration, string TargetKind,
    string? Field, string? Category, string? Code, Uuid OperationId, long ExpectedRevision,
    string? TextValue, bool? BooleanValue);
