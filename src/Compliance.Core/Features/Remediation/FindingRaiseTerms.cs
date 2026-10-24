using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The validated terms of a finding raise that the owner records inline on its raised fact.</summary>
public sealed record FindingRaiseTerms(FindingSource Source, string Title, string Description, string Severity,
    string AffectedScope, Uuid OwnerMemberId, DateOnly DueOn, ActorReference RaisedBy, DateTimeOffset RaisedAt,
    string CreationKind, string? InitialActionSha256);
