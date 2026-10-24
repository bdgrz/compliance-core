using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The validated terms of a finding triage revision.</summary>
public sealed record FindingRevision(string Severity, Uuid OwnerMemberId, DateOnly DueOn, string AffectedScope,
    string? RootCause, string Reason, ActorReference RevisedBy, DateTimeOffset RevisedAt);
