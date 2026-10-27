using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The validated terms of a finding triage revision.</summary>
public sealed record FindingRevision(string Severity, Uuid OwnerMemberId, DateOnly DueOn, string AffectedScope,
    string? RootCause, string Reason, ActorReference RevisedBy, DateTimeOffset RevisedAt);
