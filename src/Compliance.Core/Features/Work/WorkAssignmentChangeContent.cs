using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work;

/// <summary>One exact attributed change and the source identity it was authorized against; no cumulative history.</summary>
public sealed record WorkAssignmentChangeContent(WorkAssignmentSourceIdentity Source, WorkAssignmentEntryView Entry,
    DateOnly SelectionDate, Cntryl.Portia.Uuid? EffectiveAssigneeMemberId)
{
    public const string Schema = "work-assignment-change.v2";
}
