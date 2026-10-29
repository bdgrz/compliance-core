using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Projections;

static class ResponsibilitySetDirectorySchema
{
    public static readonly KvDirectory<ResponsibilitySetView, Uuid> Sets = new(
        "responsibility_sets", ComplianceCoreJsonContext.Default.ResponsibilitySetView,
        static set => set.SetId, static id => [id.ToString()], []);
}
