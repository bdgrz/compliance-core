using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>Replaces the organization's wording without changing any already launched campaign.</summary>
[Discriminator("bdgrz.policy.acknowledgement-wording.set", 1)]
public sealed record SetAcknowledgementWording(Uuid CommandId, Uuid TenantId, long ExpectedRevision,
    string Wording) : IRequest, IRbacManagementRequest, IClientManagementMutationRequest, ICallable;
