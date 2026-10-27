using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>The native acceptance of <see cref="ReserveEmail" />, the first reservation of the address.</summary>
[Discriminator("bdgrz.email-address.reserved", 1)]
public sealed record EmailAddressReserved(
    Uuid UserId,
    string EmailAddress,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
