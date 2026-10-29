using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProgramView(Uuid TenantId, Uuid ProgramId, string Name, string Stage,
    string? NextStage, long Revision, ProgramPlan Plan, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    IReadOnlyList<ProgramStageView> StagePlan)
{
    public ActorReference LastChangedBy => ActorReference.ForMember(
        LastChangedByMemberId, LastChangedByDisplay);
    public Uuid? CriteriaEditionId { get; init; }

    /// <summary>The approved Type I entry decision that moved the program into its stage.</summary>
    public Uuid? StageDecisionId { get; init; }

    public ActorReference? StageEnteredBy { get; init; }

    public DateTimeOffset? StageEnteredAt { get; init; }
}
