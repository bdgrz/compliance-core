using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>The native acceptance of <see cref="TestTenantIdentityProvider" />: a request to test one exact credential epoch.</summary>
[Discriminator("bdgrz.user-identities.tenant-identity-provider-test-requested", 1)]
public sealed record TenantIdentityProviderTestRequested(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    string TestKey,
    long CredentialRevision,
    Uuid RequestedByMemberId,
    string RequestedByDisplay,
    DateTimeOffset RequestedAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
