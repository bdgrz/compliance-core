using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Policies.Events;

[Discriminator("bdgrz.policy.draft.created", 2)]
public sealed record PolicyDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    Uuid CreateRequestId, string Identifier, ImmutableContentRef Content, string ContentSha256,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : DomainEvent, IPolicyFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public PolicyAuthorityHeader Header { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ImmutableContentRef Authority { get; init; } = default!;
    public long NativeSequence { get; init; }
}
