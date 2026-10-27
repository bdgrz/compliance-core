using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyDraftCreatedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    Uuid CreateRequestId, string Identifier, PolicyContent Content, string ContentSha256,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : IPolicyMutationContent;
