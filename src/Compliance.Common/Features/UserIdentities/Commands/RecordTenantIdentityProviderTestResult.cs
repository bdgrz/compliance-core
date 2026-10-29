using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Records a safe provider-test result from the exact named worker and request event. The command id is
///     deterministic from the request event, so a redelivered result is an exact replay.
/// </summary>
[Discriminator("bdgrz.user-identities.record-tenant-identity-provider-test-result", 1)]
public sealed record RecordTenantIdentityProviderTestResult(
    Uuid CommandId,
    Uuid TenantId,
    Uuid ProviderConnectionId,
    long ExpectedRevision,
    Uuid TestRequestEventId,
    TenantIdentityProviderTestResultContent Result,
    DateTimeOffset CompletedAt) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
