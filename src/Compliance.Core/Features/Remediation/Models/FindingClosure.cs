using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>
///     A verified closure: the authoritative finding, its accepted closure fact, the frozen closure intent and the
///     authored decision. Exactly one of <paramref name="Error" /> and the verified parts is set.
/// </summary>
public sealed record FindingClosure(RequestError? Error, FindingAggregate? Finding, NativeOperations.Models.ClosureIntent? Intent,
    ClosureDecision? Decision, Uuid TenantId, FindingHistorySnapshot? History = null)
{
    /// <summary>A failed verification.</summary>
    public static FindingClosure Failed(RequestError error) => new(error, null, null, null, Uuid.Empty);
}
