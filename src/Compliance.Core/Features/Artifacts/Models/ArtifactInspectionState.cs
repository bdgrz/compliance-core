using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
namespace Bdgrz.Compliance.Features.Artifacts.Models;

/// <summary>
/// The storage-level inspection state. Only a selected inspection engine may report a state other than
/// <see cref="NotInspected" />.
/// </summary>
public enum ArtifactInspectionState
{
    NotInspected,
    Clean,
    Quarantined,
    SecretDetected,
    Invalid,
}
