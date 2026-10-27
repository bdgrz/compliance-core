using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member.suspended", 1)]
public sealed record MemberSuspended(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid SuspendedByMemberId, string SuspendedByDisplay, DateTimeOffset SuspendedAt,
    string Reason, OperationProvenance Operation, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
