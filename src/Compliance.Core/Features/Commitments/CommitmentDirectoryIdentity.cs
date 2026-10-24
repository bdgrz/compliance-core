using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Bounded discovery/history identity; complete body remains in the native immutable source.</summary>
public sealed record CommitmentDirectoryIdentity(CommitmentAuthorityHeader Header, long NativeSequence,
    Uuid? DecisionId = null, long? Version = null, DateTimeOffset? DecidedAt = null);
