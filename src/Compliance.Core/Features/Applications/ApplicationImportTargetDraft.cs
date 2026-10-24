using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A canonical target group before its content is frozen: rows in original order and the candidate they produce.</summary>
sealed record ApplicationImportTargetDraft(Uuid ApplicationId, ulong InitialRevision,
    IReadOnlyList<ApplicationImportPlannedRow> Rows, IReadOnlyList<long> ResultingRevisions,
    ApplicationImportCandidateContent Candidate, IReadOnlyList<ApplicationImportRetirementProof> RetirementProofs);
