using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>An immutable, attributed version of a carry-forward plan.</summary>
public sealed record ProgramRolloverVersionView(
    int Version,
    ProgramRolloverContent Content,
    string ContentSha256,
    Uuid ProposedByMemberId,
    string ProposedByDisplay,
    DateTimeOffset ProposedAt,
    string ChangeReason);
