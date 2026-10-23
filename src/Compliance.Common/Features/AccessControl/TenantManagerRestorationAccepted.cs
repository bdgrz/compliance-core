using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The guard admitted a restoration or a first registration of an administrator contribution; the source is restored first and availability follows only after exact eligibility.
///     <paramref name="Owner" /> is the source stream the operation mutates, <paramref name="Revision" /> the authored
///     revision of that owner (negative when unconstrained) and <paramref name="Generation" /> the guard generation of
///     this fact.
/// </summary>
[Discriminator("bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted", 1)]
public sealed record TenantManagerRestorationAccepted(EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, ITenantManagerGuardFact;
