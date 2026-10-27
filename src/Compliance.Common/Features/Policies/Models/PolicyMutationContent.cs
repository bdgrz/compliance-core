using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyMutationContent(IPolicyMutationContent Change,
    SeparationOfDutiesWaiverProof? Waiver = null);
