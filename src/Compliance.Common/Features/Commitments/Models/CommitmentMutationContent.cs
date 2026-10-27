using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentMutationContent(IReadOnlyList<ICommitmentMutationContent> Changes,
    IReadOnlyList<SeparationOfDutiesWaiverProof>? Waivers = null, ICommitmentMutationContent? Receipt = null);
