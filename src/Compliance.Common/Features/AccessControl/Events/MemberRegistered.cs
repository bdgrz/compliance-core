using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A member was registered. The registration's logical <paramref name="CommandId" /> is the membership
///     episode id, and the fact is the native acceptance of that logical operation on the member stream.
/// </summary>
[Discriminator("bdgrz.rbac.member.registered", 1)]
public sealed record MemberRegistered(Uuid TenantId, Uuid MemberId, Uuid UserId, string Affiliation,
    Uuid MembershipEpisodeId, DateOnly? MembershipExpiresOn, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
