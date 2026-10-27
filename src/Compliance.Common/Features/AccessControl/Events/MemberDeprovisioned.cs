using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member.deprovisioned", 2)]
public sealed record MemberDeprovisioned(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid DeprovisionedByMemberId, string DeprovisionedByDisplay, DateTimeOffset DeprovisionedAt,
    string Reason, Uuid MembershipEpisodeId, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
