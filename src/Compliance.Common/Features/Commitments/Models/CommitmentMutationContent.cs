using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentMutationContent(IReadOnlyList<ICommitmentMutationContent> Changes,
    IReadOnlyList<SeparationOfDutiesWaiverProof>? Waivers = null, ICommitmentMutationContent? Receipt = null);
