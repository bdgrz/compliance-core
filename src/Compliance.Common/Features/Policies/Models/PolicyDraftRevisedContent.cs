using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyDraftRevisedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, PolicyContent Content, string ContentSha256, long? PredecessorVersion,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : IPolicyMutationContent;
