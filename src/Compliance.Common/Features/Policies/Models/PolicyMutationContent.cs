using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyMutationContent(IPolicyMutationContent Change,
    SeparationOfDutiesWaiverProof? Waiver = null);
