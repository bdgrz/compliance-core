using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Artifacts.Models;

/// <summary>
/// The outcome of inspecting one content reference.
/// </summary>
public sealed record ArtifactInspectionResult(ArtifactInspectionState State);
