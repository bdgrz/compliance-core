using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyMutationContent(IPolicyMutationContent Change,
    SeparationOfDutiesWaiverProof? Waiver = null);
