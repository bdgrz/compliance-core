using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     The complete, attributed amendment frozen at admission. The apply step derives the new period state from it
///     and the period as it is then; nothing is re-read from the request.
/// </summary>
public sealed record TypeIIPeriodAmendmentChange(DateOnly PeriodStart, DateOnly PeriodEnd, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset AmendedAt)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "type-ii-period-amendment-change.v1";
}
