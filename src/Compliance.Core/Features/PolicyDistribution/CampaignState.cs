using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A campaign as its owner and participants say it is: the replayed audience of the campaign and the applied
///     results of every participant that has any. A result that is admitted but not yet applied does not count; the
///     closure drains them before it freezes the totals.
/// </summary>
sealed class CampaignState(PolicyCampaignSnapshot campaign, CampaignRoster roster,
    IReadOnlyList<string> teams, IReadOnlyDictionary<Uuid, PolicyCampaignParticipantSnapshot> participants,
    CampaignTotalsView? closingTotals)
{
    public PolicyCampaignSnapshot Campaign { get; } = campaign;

    public CampaignRoster Roster { get; } = roster;

    public IReadOnlyList<string> Teams { get; } = teams;

    public IReadOnlyDictionary<Uuid, PolicyCampaignParticipantSnapshot> Participants { get; } = participants;

    public string? DisplayNameOf(Uuid personId) => Roster.Find(personId)?.DisplayName;

    public static CampaignAcknowledgementView ViewOf(PolicyAcknowledgementRecorded ev) => new(ev.AcknowledgementId,
        ev.PersonId, ev.PolicyVersion, ev.ContentSha256, ev.AcknowledgementText, ev.Performer, ev.Recorder,
        ev.RecordedOnBehalf, ev.AcknowledgedAt);

    public static CampaignCompletionView ViewOf(TrainingCompletionRecorded ev) => new(ev.CompletionId, ev.PersonId,
        ev.RequirementVersion, ev.CompletedOn, ev.Source, ev.EvidenceReference, ev.Recorder, ev.RecordedAt);

    public static CampaignWaiverView ViewOf(CampaignWaiverApproved ev) => new(ev.WaiverId, ev.PersonId, ev.Reason,
        ev.ExpiresOn, ev.Approver, ev.ApprovedAt);

    /// <summary>The person's state as of a date; results recorded later do not count.</summary>
    public string StateOf(CampaignParticipant member, DateOnly asOf)
    {
        if (!member.IsActive)
            return "removed";
        var applied = Participants.GetValueOrDefault(member.PersonId);
        if (applied?.Acknowledgement is { } acknowledgement &&
            DateOnly.FromDateTime(acknowledgement.AcknowledgedAt.UtcDateTime) <= asOf)
            return "acknowledged";
        if (applied?.Completion is { } completion && completion.CompletedOn <= asOf &&
            DateOnly.FromDateTime(completion.RecordedAt.UtcDateTime) <= asOf)
            return "completed";
        if (applied?.Waivers.Count > 0 && applied.Waivers[^1] is { } exception &&
            DateOnly.FromDateTime(exception.ApprovedAt.UtcDateTime) <= asOf && asOf < exception.ExpiresOn)
            return "excepted";
        return asOf > member.DueOn ? "overdue" : "pending";
    }

    public CampaignTotalsView Totals(DateOnly asOf)
    {
        var states = Roster.Order.Select(member => StateOf(member, asOf)).ToArray();
        var launch = Roster.Order.Count(static member => member.LaunchMember);
        return new CampaignTotalsView(Roster.Order.Count, launch, Roster.Order.Count - launch,
            states.Count(static state => state == "removed"),
            states.Count(static state => state == "pending"),
            states.Count(static state => state == "overdue"),
            states.Count(static state => state is "acknowledged" or "completed"),
            states.Count(static state => state == "excepted"));
    }

    public CampaignView ToView(DateOnly asOf)
    {
        var launch = Campaign.Launch ?? throw new InvalidOperationException("The campaign was not launched.");
        var closed = Campaign.Closing;
        return new CampaignView(Campaign.TenantId, launch.ProgramId, Campaign.Id, launch.Subject,
            launch.AudienceKind, Teams, launch.RosterSnapshotId, launch.RosterContentSha256,
            Campaign.LatestRosterSnapshotId, launch.LaunchedOn, launch.DueOn, launch.Instructions,
            launch.AcknowledgementText, closed is not null ? "closed" : Campaign.IsSealed ? "closing" : "open", asOf,
            Totals(asOf), launch.Actor, launch.LaunchedAt, closed?.Actor, closed?.ClosedAt, closingTotals,
            Campaign.Revision);
    }

    public IReadOnlyList<CampaignParticipantView> ParticipantViews(DateOnly asOf, string? state) =>
        Roster.Order.Select(member =>
            {
                var applied = Participants.GetValueOrDefault(member.PersonId);
                return new CampaignParticipantView(member.PersonId, member.DisplayName, member.WorkerType,
                    member.Department, member.Inclusion, member.IncludedBy, StateOf(member, asOf), member.DueOn,
                    member.RemovedReason, member.RemovedBy,
                    applied?.Acknowledgement is { } ack ? ViewOf(ack) : null,
                    applied?.Completion is { } completion ? ViewOf(completion) : null,
                    applied?.Waivers.Count > 0 ? ViewOf(applied.Waivers[^1]) : null);
            })
            .Where(view => state is null || view.State == state)
            .ToArray();
}
