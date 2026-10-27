using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyDraftDiscardedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, string Rationale, ActorReference Actor, DateTimeOffset DiscardedAt) : IPolicyMutationContent;
