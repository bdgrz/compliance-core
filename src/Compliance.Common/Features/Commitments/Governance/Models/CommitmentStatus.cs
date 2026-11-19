using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Models;
public sealed record CommitmentStatus(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, string Kind,
    string Identifier, long Revision, CommitmentTerms? OpenDraft, IReadOnlyList<ApprovedCommitment> Versions,
    DateOnly? RetiredOn, IReadOnlyList<string> OutOfScopeCategories);
