using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

static class CommitmentDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<CommitmentDirectoryIdentity> ByDraftRevision = new("by_draft_revision", 1,
        static value => [value.Header.DraftId.ToString(), value.Header.Revision.ToString("D20", CultureInfo.InvariantCulture)]);
    public static readonly KvDirectory<CommitmentDirectoryIdentity, string> Revisions = new("commitment_history_revision_identities",
        ComplianceCoreJsonContext.Default.CommitmentDirectoryIdentity, static value => RevisionKey(value.Header.DraftId, value.Header.Revision),
        static key => [key], [ByDraftRevision]);
    public static string RevisionKey(Uuid draftId, long revision) => $"{draftId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
