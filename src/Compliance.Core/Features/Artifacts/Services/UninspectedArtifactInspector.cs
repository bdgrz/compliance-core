using Bdgrz.Compliance.Features.Artifacts.Contracts;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Artifacts.Services;

/// <summary>
/// The default inspector. It never reports content as clean, because no inspection engine is selected.
/// </summary>
public sealed class UninspectedArtifactInspector : IArtifactInspector
{
    public ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
        CancellationToken ct = default) =>
        ValueTask.FromResult(new ArtifactInspectionResult(ArtifactInspectionState.NotInspected));
}
