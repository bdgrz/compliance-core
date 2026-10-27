using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyDraftRevisedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, PolicyContent Content, string ContentSha256, long? PredecessorVersion,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : IPolicyMutationContent;
