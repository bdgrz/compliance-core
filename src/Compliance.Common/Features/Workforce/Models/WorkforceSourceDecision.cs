using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>A reconciliation decision bound to one checked canonical revision.</summary>
public sealed record WorkforceSourceDecision(string Outcome, string Note, long TargetRevision,
    ActorReference Actor, DateTimeOffset DecidedAt);
