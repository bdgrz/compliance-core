using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The source change a withdrawal admitted was applied; the withdrawal operation is settled and its reason stays withheld.
///     <paramref name="Owner" /> is the source stream the operation mutates, <paramref name="Revision" /> the authored
///     revision of that owner (negative when unconstrained) and <paramref name="Generation" /> the guard generation of
///     this fact.
/// </summary>
[Discriminator("bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed", 2)]
public sealed record TenantManagerWithdrawalCompleted(EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult, TenantManagerEpisodeAuthority CurrentAuthority = null!) : DomainEvent, ITenantManagerGuardFact;
