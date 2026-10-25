namespace Bdgrz.Compliance.Features.Work;

/// <summary>Complete imported per-item legacy chronology with its exact read-only source cut.</summary>
public sealed record WorkAssignmentLegacyContent(WorkAssignmentState State, WorkAssignmentLegacyProof Proof)
{
    public const string Schema = "work-assignment-legacy-history.v2";
}
