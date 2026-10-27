using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyDraftRevisedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, PolicyContent Content, string ContentSha256, long? PredecessorVersion,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : IPolicyMutationContent;
