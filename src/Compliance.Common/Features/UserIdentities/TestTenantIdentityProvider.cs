using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Requests a test of the current credential of one connection. <paramref name="TestKey" /> is the caller's key
///     for the test; the logical identity of the operation is derived from the connection and the exact key, so an
///     exact retry replays the original request and a reused key on a different revision conflicts.
/// </summary>
[Discriminator("bdgrz.user-identities.test-tenant-identity-provider", 2)]
public sealed record TestTenantIdentityProvider(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    long ExpectedRevision,
    string TestKey) : IRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => TestId(ProviderConnectionId, TestKey);

    /// <summary>The logical identity of a test request: a UUIDv5 of the exact key under the connection.</summary>
    public static Uuid TestId(Uuid providerConnectionId, string testKey) =>
        Uuid.CreateVersion5(providerConnectionId, Encoding.UTF8.GetBytes(testKey ?? string.Empty));
}
