using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyDraftDiscardedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, string Rationale, ActorReference Actor, DateTimeOffset DiscardedAt) : IPolicyMutationContent;
