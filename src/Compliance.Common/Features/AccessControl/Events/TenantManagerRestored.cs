using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The guard restored a member's availability as an administrator for <paramref name="Reason" /> after the exact
///     source restoration and current eligibility were proven.
/// </summary>
[Discriminator("bdgrz.rbac.manager_guard.restored", 2)]
public sealed record TenantManagerRestored(Uuid TenantId, Uuid MemberId, string Reason, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, TenantManagerEpisodeAuthority CurrentAuthority = null!)
    : DomainEvent, ITenantManagerAuthorityFact;
