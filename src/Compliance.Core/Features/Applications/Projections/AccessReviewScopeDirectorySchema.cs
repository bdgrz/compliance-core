using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Projections;

static class AccessReviewScopeDirectorySchema
{
    public static readonly KvDirectory<AccessReviewScopeRecord, Uuid> Scopes = new(
        "access_review_scopes", ComplianceCoreJsonContext.Default.AccessReviewScopeRecord,
        static scope => scope.SystemInstanceId, static id => [id.ToString()], []);
}
