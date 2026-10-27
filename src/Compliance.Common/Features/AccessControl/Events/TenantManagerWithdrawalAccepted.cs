using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

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
