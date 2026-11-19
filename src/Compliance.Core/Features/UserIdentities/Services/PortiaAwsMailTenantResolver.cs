using Cntryl.Portia;
using Cntryl.Portia.Communications.Aws;
using Bdgrz.Compliance.Features.UserIdentities.Models;

namespace Bdgrz.Compliance.Features.UserIdentities.Services;

sealed class PortiaAwsMailTenantResolver(EmailChallengeDeliverySettings settings) : IAwsMailTenantResolver
{
    public ValueTask<AwsMailIdentity> ResolveAsync(TenantId tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ = tenantId;
        return ValueTask.FromResult(new AwsMailIdentity(settings.FromAddress!));
    }
}
