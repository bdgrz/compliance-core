using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>A deserialized native event together with its fully populated locator.</summary>
public sealed record ResolvedSourceFact(DomainEvent Event, SourceFactLocator Locator);
