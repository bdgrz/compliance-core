using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
namespace Bdgrz.Compliance.Features.Artifacts.Contracts;

/// <summary>
/// Inspection hook for stored content, such as validation or malware scanning.
/// </summary>
public interface IArtifactInspector
{
    ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
        CancellationToken ct = default);
}
