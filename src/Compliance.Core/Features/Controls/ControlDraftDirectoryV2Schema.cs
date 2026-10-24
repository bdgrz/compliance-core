using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

static class ControlDraftDirectoryV2Schema
{
    public static readonly KvDirectoryIndex<ControlDirectoryIdentity> ByProgramIdentifier = new(
        "by_program_identifier", 1, static control => [control.ProgramId.ToString(), control.Identifier]);
    public static readonly KvDirectory<ControlDirectoryIdentity, Uuid> Controls = new("control_identities",
        ComplianceCoreJsonContext.Default.ControlDirectoryIdentity, static value => value.ControlId,
        static id => [id.ToString()], [ByProgramIdentifier]);
    public static readonly KvDirectory<ControlRevisionIdentity, string> Revisions = new("control_revision_identities",
        ComplianceCoreJsonContext.Default.ControlRevisionIdentity, static value => RevisionKey(value.ControlId, value.Revision),
        static key => [key], []);
    public static string RevisionKey(Uuid controlId, long revision) => $"{controlId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
