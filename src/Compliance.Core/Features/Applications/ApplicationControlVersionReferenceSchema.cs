using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationControlVersionReferenceSchema
{
    public static readonly KvDirectoryIndex<ApplicationControlVersionReferenceView> ByRecord =
        new("by_governed_record", 1, static reference =>
        [reference.SubjectType, reference.GovernedRecordId.ToString(),
            reference.ControlId.ToString(), reference.VersionId.ToString(),
            reference.EntryId.ToString()]);

    public static readonly KvDirectoryIndex<ApplicationControlVersionReferenceView>
        ByControlVersion = new("by_control_version", 1, static reference =>
        [reference.ControlId.ToString(), reference.VersionId.ToString(),
            reference.EntryId.ToString()]);

    public static readonly KvDirectory<ApplicationControlVersionReferenceView,
        (Uuid ControlId, Uuid VersionId, Uuid EntryId)> References = new(
        "application_control_version_references",
        ComplianceCoreJsonContext.Default.ApplicationControlVersionReferenceView,
        static reference => (reference.ControlId, reference.VersionId, reference.EntryId),
        static key => [key.ControlId.ToString(), key.VersionId.ToString(),
            key.EntryId.ToString()], [ByRecord, ByControlVersion]);

    public static readonly KvDirectory<ApplicationControlVersionReferenceState, Uuid> States =
        new("application_control_version_reference_states",
            ComplianceCoreJsonContext.Default.ApplicationControlVersionReferenceState,
            static state => state.ControlId, static id => [id.ToString()], []);
}
