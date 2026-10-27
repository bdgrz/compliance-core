using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.deprovisioned", 2)]
public sealed record MemberDeprovisioned(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid DeprovisionedByMemberId, string DeprovisionedByDisplay, DateTimeOffset DeprovisionedAt,
    string Reason, Uuid MembershipEpisodeId, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
