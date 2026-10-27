using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

public sealed record SystemDescriptionResolvedSources(SystemDescriptionScope Scope,
    IReadOnlyList<SystemDescriptionSourceReference> Sources, ImmutableContentRef Proof);
