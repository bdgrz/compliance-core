using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Client-authored structural change of one edition entry.</summary>
public sealed record CriterionChangeIntent(Uuid EditionId, Uuid DraftGeneration, string Identifier,
    Uuid OperationId, long ExpectedRevision, string Action, string? Kind, string? Category,
    string? ParentIdentifier, Uuid? ParentIncarnationId, string? Summary);
