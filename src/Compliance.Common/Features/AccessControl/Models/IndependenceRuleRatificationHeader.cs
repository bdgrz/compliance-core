using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Current monotonic activation authority; personal evidence remains immutable content.</summary>
public sealed record IndependenceRuleRatificationHeader(long Sequence, Uuid RatificationId, long RuleVersion,
    string RuleContentDigest, ActorReference Actor, DateTimeOffset RecordedAt);
