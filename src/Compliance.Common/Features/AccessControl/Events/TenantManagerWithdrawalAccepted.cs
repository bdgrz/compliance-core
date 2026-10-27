using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The guard admitted a withdrawal of an administrator contribution before its source change: availability is withdrawn at this fact, and the source mutation follows through the administrator transition process.
///     <paramref name="Owner" /> is the source stream the operation mutates, <paramref name="Revision" /> the authored
///     revision of that owner (negative when unconstrained) and <paramref name="Generation" /> the guard generation of
///     this fact.
/// </summary>
[Discriminator("bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted", 2)]
public sealed record TenantManagerWithdrawalAccepted(EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult, TenantManagerEpisodeAuthority CurrentAuthority = null!) : DomainEvent, ITenantManagerGuardFact;
