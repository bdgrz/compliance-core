using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>One version retains its ratified classifications permanently.</summary>
public sealed record IndependenceRuleSetView(long Version, long Revision, IndependenceRuleSetTerms Terms,
    bool IsRatified, Uuid ActorUserId, DateTimeOffset RecordedAt);
