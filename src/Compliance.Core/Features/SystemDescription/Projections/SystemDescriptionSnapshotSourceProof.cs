using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Projections;

/// <summary>Exact authoritative native source cut used to freeze a complete description source-version population.</summary>
public sealed record SystemDescriptionSnapshotSourceProof(SystemDescriptionScope Scope,
    IReadOnlyList<SystemDescriptionSourceReference> Sources, IReadOnlyList<SystemDescriptionSnapshotSourceFact> Facts);
