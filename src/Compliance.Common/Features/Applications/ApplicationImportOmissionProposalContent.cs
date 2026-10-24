using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The complete content of a sealed omission proposal: the exact source snapshot it was prepared against, the
///     present source record identities and every proposed retirement row. It is immutable content referenced by the
///     seal fact and by the frozen plan.
/// </summary>
public sealed record ApplicationImportOmissionProposalContent(string SourceKey, string SourceNamespace,
    ulong SourcePosition, string ContentSha256, IReadOnlyList<string> PresentSourceRecordIds, Uuid MemberId,
    string MemberDisplay, string Reason, DateTimeOffset PreparedAt, IReadOnlyList<ApplicationImportRetirementRow> Rows)
{
    /// <summary>Schema of the proposal content.</summary>
    public const string Schema = "ApplicationImportRetirementProposalSealed.Proposal.v1";
}
