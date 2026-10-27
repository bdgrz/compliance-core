using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member.reinstated", 1)]
public sealed record MemberReinstated(Uuid TenantId, Uuid MemberId, Uuid UserId,
    string Affiliation, Uuid ReinstatedByMemberId, string ReinstatedByDisplay,
    DateTimeOffset ReinstatedAt, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
