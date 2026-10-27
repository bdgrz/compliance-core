using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Current monotonic activation authority; personal evidence remains immutable content.</summary>
public sealed record IndependenceRuleRatificationHeader(long Sequence, Uuid RatificationId, long RuleVersion,
    string RuleContentDigest, ActorReference Actor, DateTimeOffset RecordedAt);
