using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
namespace Bdgrz.Compliance.Features.Artifacts.Models;

/// <summary>
/// An opaque, short-lived delivery location for verified content. Callers must not parse the location.
/// </summary>
public sealed record ArtifactDelivery(ArtifactContentReference Content, Uri Location, DateTimeOffset ExpiresAt);
