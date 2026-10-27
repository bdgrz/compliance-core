using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Current sequence and digest authority of the global authored rule catalog.</summary>
public sealed record IndependenceRuleCatalogHeader(long Sequence, long RuleVersion, string RuleContentDigest,
    ActorReference Actor, DateTimeOffset RecordedAt);
