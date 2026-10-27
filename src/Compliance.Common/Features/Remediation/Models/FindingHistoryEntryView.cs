using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>One attributable change to a finding.</summary>
public sealed record FindingHistoryEntryView(long Revision, string Change, string Summary,
    ActorReference Actor, DateTimeOffset At);
