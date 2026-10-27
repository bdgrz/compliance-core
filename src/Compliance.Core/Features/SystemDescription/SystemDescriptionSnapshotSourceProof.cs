using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Exact authoritative native source cut used to freeze a complete description source-version population.</summary>
public sealed record SystemDescriptionSnapshotSourceProof(SystemDescriptionScope Scope,
    IReadOnlyList<SystemDescriptionSourceReference> Sources, IReadOnlyList<SystemDescriptionSnapshotSourceFact> Facts);
