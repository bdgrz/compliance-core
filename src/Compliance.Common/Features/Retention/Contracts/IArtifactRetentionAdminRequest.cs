using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Retention.Contracts;

public interface IArtifactRetentionAdminRequest : IRequestBase
{
    Uuid TenantId { get; }
}
