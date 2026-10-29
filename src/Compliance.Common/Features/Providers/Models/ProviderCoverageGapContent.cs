using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.Providers.Models;

/// <summary>Exact provider coverage facts and the source revision that supports them.</summary>
public sealed record ProviderCoverageGapContent(Uuid ServiceId, string Service,
    string Assertion, DateOnly PeriodStart, DateOnly PeriodEnd, string SourceKind,
    Uuid SourceId, long SourceRevision, string Description);
