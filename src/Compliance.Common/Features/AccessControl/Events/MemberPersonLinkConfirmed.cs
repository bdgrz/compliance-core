using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member.person-link.confirmed", 1)]
public sealed record MemberPersonLinkConfirmed(Uuid TenantId, Uuid MemberId, Uuid UserId, Uuid PersonId, PersonMemberLink Link, NativeFactReference Source,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
