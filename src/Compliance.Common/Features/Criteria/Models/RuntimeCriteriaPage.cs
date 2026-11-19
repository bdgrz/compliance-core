using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

// Coverage describes the projected snapshot, not the current authoritative source head.

/// <summary>A bounded page bound to the publication fact and projection generation.</summary>
public sealed record RuntimeCriteriaPage(Uuid EditionId, Uuid? PublicationEventId, string Generation,
    string Coverage, IReadOnlyList<RuntimeCriteriaEntry> Items, string? NextCursor);
