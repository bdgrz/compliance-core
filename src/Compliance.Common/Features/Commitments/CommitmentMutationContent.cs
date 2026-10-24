using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentMutationContent(IReadOnlyList<ICommitmentMutationContent> Changes,
    IReadOnlyList<SeparationOfDutiesWaiverProof>? Waivers = null, ICommitmentMutationContent? Receipt = null);
