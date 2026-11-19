using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Models;
public sealed record ApprovedCommitment(long Version, long DraftRevision, CommitmentTerms Terms,
    DateOnly EffectiveOn, Uuid BoundaryVersionId, Uuid ApprovedBy, DateTimeOffset ApprovedAt, Uuid? WaiverId);
