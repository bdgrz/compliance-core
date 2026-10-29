using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Complete imported per-item legacy chronology with its exact read-only source cut.</summary>
public sealed record WorkAssignmentLegacyContent(WorkAssignmentState State, WorkAssignmentLegacyProof Proof)
{
    public const string Schema = "work-assignment-legacy-history.v2";
}
