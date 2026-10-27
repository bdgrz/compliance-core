using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.access-grant.issue", 1)]
public sealed record GrantAccess(Uuid TenantId, Uuid GrantId, AccessGrantProposal Proposal)
    : IRequest, IClientRbacMutationRequest, ICallable;
