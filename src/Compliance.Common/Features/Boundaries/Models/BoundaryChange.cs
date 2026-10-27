using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryChange(string Field, string ChangeType, Uuid? EntryId,
    string? PreviousValue, string? ProposedValue,
    BoundaryScopeEntry? PreviousEntry, BoundaryScopeEntry? ProposedEntry);
