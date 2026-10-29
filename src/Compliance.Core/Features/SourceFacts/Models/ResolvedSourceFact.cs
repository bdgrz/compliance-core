using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.SourceFacts.Models;

/// <summary>A deserialized native event together with its fully populated locator.</summary>
public sealed record ResolvedSourceFact(DomainEvent Event, SourceFactLocator Locator);
