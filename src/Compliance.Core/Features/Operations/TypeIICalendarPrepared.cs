using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One exact immutable before/after cut and its transient terminal membership evidence.</summary>
public sealed record TypeIICalendarPrepared(TypeIICalendarAuthority Before, TypeIICalendarAuthority After,
    ImmutableContentRef Authority, IReadOnlyList<TypeIICalendarHistoryRead> History,
    TypeIICalendarHistoryNode? NewHistory, object Change);
