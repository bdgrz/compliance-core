using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Services;

static class WorkforceObservationResolutionSchema
{
    public static readonly KvDirectory<WorkforceObservationResolutionView, Uuid> Resolutions = new(
        "resolutions", ComplianceCoreJsonContext.Default.WorkforceObservationResolutionView,
        static view => view.ObservationId,
        static observationId => [observationId.ToString()], []);
}
