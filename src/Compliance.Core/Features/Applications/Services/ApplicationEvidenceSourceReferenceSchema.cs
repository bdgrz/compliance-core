using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Services;

static class ApplicationEvidenceSourceReferenceSchema
{
    public static readonly KvDirectoryIndex<ApplicationEvidenceSourceReferenceView> ByControl =
        new("by_control", 1, static reference =>
        [reference.ControlId.ToString(), reference.EvidenceSupportId.ToString()]);

    public static readonly KvDirectory<ApplicationEvidenceSourceReferenceView,
        (Uuid ControlId, Uuid EvidenceSupportId)> References = new(
        "application_evidence_source_references",
        ComplianceCoreJsonContext.Default.ApplicationEvidenceSourceReferenceView,
        static reference => (reference.ControlId, reference.EvidenceSupportId),
        static key => [key.ControlId.ToString(), key.EvidenceSupportId.ToString()], [ByControl]);

    public static readonly KvDirectory<ApplicationEvidenceSourceReferenceCoverage, Uuid> Coverage =
        new("application_evidence_source_reference_coverage",
            ComplianceCoreJsonContext.Default.ApplicationEvidenceSourceReferenceCoverage,
            static state => state.ProgramId, static programId => [programId.ToString()], []);
}
