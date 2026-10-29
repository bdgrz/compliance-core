using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Commitments.Projections;

static class CommitmentDraftDirectorySchema
{
    public static readonly KvDirectoryIndex<CommitmentDirectoryIdentity> ByProgramKindIdentifier = new(
        "by_program_kind_identifier", 1, static value => [value.Header.ProgramId.ToString(), value.Header.Kind, value.Header.Identifier]);
    public static readonly KvDirectory<CommitmentDirectoryIdentity, Uuid> Drafts = new("commitment_draft_identities",
        ComplianceCoreJsonContext.Default.CommitmentDirectoryIdentity, static value => value.Header.DraftId,
        static id => [id.ToString()], [ByProgramKindIdentifier]);
    public static readonly KvDirectory<CommitmentDirectoryIdentity, string> Revisions = new("commitment_revision_identities",
        ComplianceCoreJsonContext.Default.CommitmentDirectoryIdentity, static value => RevisionKey(value.Header.DraftId, value.Header.Revision),
        static key => [key], []);
    public static readonly KvDirectoryIndex<CommitmentDirectoryIdentity> ByDraftVersion = new("by_draft_version", 1,
        static value => [value.Header.DraftId.ToString(), value.Version!.Value.ToString("D20", CultureInfo.InvariantCulture)]);
    public static readonly KvDirectory<CommitmentDirectoryIdentity, string> Versions = new("commitment_version_identities",
        ComplianceCoreJsonContext.Default.CommitmentDirectoryIdentity, static value => RevisionKey(value.Header.DraftId, value.Version!.Value),
        static key => [key], [ByDraftVersion]);
    public static readonly KvDirectoryIndex<CommitmentDirectoryIdentity> ByDraftDecision = new("by_draft_decided_at", 1,
        static value => [value.Header.DraftId.ToString(), value.DecidedAt!.Value.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            value.DecisionId!.Value.ToString()]);
    public static readonly KvDirectory<CommitmentDirectoryIdentity, string> Decisions = new("commitment_decision_identities",
        ComplianceCoreJsonContext.Default.CommitmentDirectoryIdentity, static value => DecisionKey(value.Header.DraftId, value.DecisionId!.Value),
        static key => [key], [ByDraftDecision]);
    public static string DecisionKey(Uuid draftId, Uuid decisionId) => $"{draftId}:{decisionId}";
    public static string RevisionKey(Uuid draftId, long revision) => $"{draftId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
