using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>The kind of engagement independence operation a frozen intent admits.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EngagementIndependenceOperationKind>))]
public enum EngagementIndependenceOperationKind
{
    /// <summary>The acceptance of a draft engagement with its proposed professional team.</summary>
    [JsonStringEnumMemberName("acceptance")]
    Acceptance,

    /// <summary>One positive actual staff assignment to an accepted engagement.</summary>
    [JsonStringEnumMemberName("staff_assignment")]
    StaffAssignment,
}
