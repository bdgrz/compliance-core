using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member.person-link.refused", 1)]
public sealed record MemberPersonLinkRefused(Uuid TenantId, Uuid MemberId, Uuid UserId, Uuid PersonId, Uuid LinkId, long ExpectedGeneration, long Generation, string Reason,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
