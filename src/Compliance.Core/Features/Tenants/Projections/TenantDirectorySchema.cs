using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Projections;

/// <summary>The shared tenant directory schema, used by both the read and write sides.</summary>
static class TenantDirectorySchema
{
    public static readonly KvDirectory<TenantView, Uuid> Directory = new(
        "tenants-by-id",
        ComplianceCoreJsonContext.Default.TenantView,
        static tenant => tenant.TenantId,
        static tenantId => [tenantId.ToString()],
        []);
}
