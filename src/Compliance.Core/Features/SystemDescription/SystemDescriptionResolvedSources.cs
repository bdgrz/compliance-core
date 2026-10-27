using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed record SystemDescriptionResolvedSources(SystemDescriptionScope Scope,
    IReadOnlyList<SystemDescriptionSourceReference> Sources, ImmutableContentRef Proof);
