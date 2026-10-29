using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Services;

static class AccessReviewScopeStreams
{
    public const string Area = "system-instance-access-review-scopes";
    public const string ProjectorName = "AccessReviewScopeDirectoryV2";

    public static EventStreamPattern TenantPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), Area);
}
