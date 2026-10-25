using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed record SystemDescriptionResolvedSources(SystemDescriptionScope Scope,
    IReadOnlyList<SystemDescriptionSourceReference> Sources, ImmutableContentRef Proof);
