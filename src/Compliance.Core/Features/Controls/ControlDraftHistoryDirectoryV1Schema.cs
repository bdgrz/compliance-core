using Bdgrz.Compliance.Features.Controls.Models;
using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

static class ControlDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<ControlRevisionIdentity> ByControlRevision = new("by_control_revision", 1,
        static value => [value.ControlId.ToString(), value.Revision.ToString("D20", CultureInfo.InvariantCulture)]);
    public static readonly KvDirectory<ControlRevisionIdentity, string> Revisions = new("control_revision_identities",
        ComplianceCoreJsonContext.Default.ControlRevisionIdentity, static value => RevisionKey(value.ControlId, value.Revision),
        static key => [key], [ByControlRevision]);
    public static string RevisionKey(Uuid controlId, long revision) => $"{controlId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
