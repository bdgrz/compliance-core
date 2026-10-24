using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     A new draft revision. <c>PredecessorVersion</c> is set when the revision opens a successor
///     draft from that approved version.
/// </summary>
[Discriminator("bdgrz.policy.draft.revised", 2)]
public sealed record PolicyDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, ImmutableContentRef Content, string ContentSha256, long? PredecessorVersion,
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
