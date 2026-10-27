using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>
///     The native acceptance of <see cref="RecordTenantIdentityProviderTestResult" />: a bounded safe test result for
///     the exact request event and credential epoch. Provider tokens and response bodies are never persisted.
/// </summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-tested", 3)]
public sealed record TenantIdentityProviderTested(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    Uuid TestRequestEventId,
    long CredentialRevision,
    ActorReference TestedByActor,
    TenantIdentityProviderTestResultContent Result,
    DateTimeOffset TestedAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
