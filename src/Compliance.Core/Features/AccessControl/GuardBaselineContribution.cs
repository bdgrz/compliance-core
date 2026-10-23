using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>One administrators-team assignment of a member episode as found in the source cut.</summary>
public sealed record GuardBaselineContribution(Uuid MemberId, Uuid MemberEpisodeId, Uuid AssignmentId,
    long AssignmentGeneration, IReadOnlyList<string> Reasons);
