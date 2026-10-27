using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>One immutable Type II plan version and its proposer attribution.</summary>
public sealed record TypeIIPlanVersionView(
    int Version,
    TypeIIPlanContent Content,
    string ContentSha256,
    Uuid ProposedByMemberId,
    string ProposedByDisplay,
    DateTimeOffset ProposedAt,
    string ChangeReason);
