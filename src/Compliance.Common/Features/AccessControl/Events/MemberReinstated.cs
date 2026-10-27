using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.reinstated", 1)]
public sealed record MemberReinstated(Uuid TenantId, Uuid MemberId, Uuid UserId,
    string Affiliation, Uuid ReinstatedByMemberId, string ReinstatedByDisplay,
    DateTimeOffset ReinstatedAt, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
