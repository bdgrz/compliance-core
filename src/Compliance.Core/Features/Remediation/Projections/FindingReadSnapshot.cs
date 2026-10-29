using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Projections;

/// <summary>A captured read-side finding inventory; this is never retained as aggregate command state.</summary>
public sealed class FindingReadSnapshot(
    IReadOnlyList<FindingView> findings,
    IReadOnlyDictionary<Uuid, IReadOnlyList<FindingClosureView>> closures)
{
    /// <summary>Every finding of the program, oldest first.</summary>
    public IReadOnlyList<FindingView> Findings { get; } = findings;

    /// <summary>True when the program has this finding.</summary>
    public bool Contains(Uuid findingId) => Findings.Any(finding => finding.FindingId == findingId);

    /// <summary>The finding, or null.</summary>
    public FindingView? Read(Uuid findingId) => Findings.FirstOrDefault(finding => finding.FindingId == findingId);

    /// <summary>Every closure the finding recorded, across reopened generations.</summary>
    public IReadOnlyList<FindingClosureView> ReadClosures(Uuid findingId) => closures.GetValueOrDefault(findingId) ?? [];
}
