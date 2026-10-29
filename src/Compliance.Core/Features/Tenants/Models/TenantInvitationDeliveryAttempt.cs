using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>One provider send attempt of an invitation and the result the provider returned for it.</summary>
public sealed record TenantInvitationDeliveryAttempt(Uuid AttemptId, DateTimeOffset StartedAt, string Result,
    string? FailureCode, DateTimeOffset? ResultAt)
{
    /// <summary>The send began and no result is stored; a resume sends the same attempt again.</summary>
    public const string InFlight = "in_flight";

    /// <summary>The provider accepted the message.</summary>
    public const string Accepted = "accepted";

    /// <summary>The send definitely failed.</summary>
    public const string Rejected = "rejected";
}
