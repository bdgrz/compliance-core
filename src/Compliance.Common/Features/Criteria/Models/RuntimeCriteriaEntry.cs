using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One entry's published-cut status. Pending is not proof of absence.</summary>
public sealed record RuntimeCriteriaEntry(Uuid EditionId, string Identifier, string Status,
    Uuid? IncarnationId, string? Kind, string? Category, string? ParentIdentifier,
    Uuid? ParentIncarnationId, string? Summary);
