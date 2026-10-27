using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>One attributable change to a finding.</summary>
public sealed record FindingHistoryEntryView(long Revision, string Change, string Summary,
    ActorReference Actor, DateTimeOffset At);
