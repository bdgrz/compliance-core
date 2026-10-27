using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Services;

/// <summary>
///     The audience of one campaign as replayed from its facts: the frozen launch audience and every attributed
///     amendment, each read from verified immutable content. People who left keep their place and their history.
/// </summary>
sealed class CampaignRoster
{
    readonly Dictionary<Uuid, CampaignParticipant> _participants = [];
    readonly List<CampaignParticipant> _order = [];
    readonly List<CampaignAmendmentView> _amendments = [];

    CampaignRoster()
    {
    }

    /// <summary>Everyone ever included, in inclusion order.</summary>
    public IReadOnlyList<CampaignParticipant> Order => _order;

    /// <summary>Every attributed amendment in order.</summary>
    public IReadOnlyList<CampaignAmendmentView> Amendments => _amendments;

    public CampaignParticipant? Find(Uuid personId) => _participants.GetValueOrDefault(personId);

    public CampaignParticipant? FindActive(Uuid personId) =>
        _participants.TryGetValue(personId, out var participant) && participant.IsActive ? participant : null;

    public static async Task<CampaignRoster> ReadAsync(IImmutableContentStore content,
        PolicyCampaignSnapshot campaign, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(campaign);
        var launch = campaign.Launch ?? throw new InvalidOperationException("The campaign was not launched.");
        var frozen = campaign.Frozen ?? throw new InvalidOperationException("The campaign audience was not frozen.");
        var realm = campaign.Stream.Realm;
        var roster = new CampaignRoster();
        var entries = await content.ReadJsonAsync<IReadOnlyList<CampaignAudienceEntry>>(realm,
            frozen.EntriesManifest, ct).ConfigureAwait(false);
        var frozenFact = NativeFactReference.Of(campaign.Stream, frozen);
        foreach (var entry in entries)
            roster.Add(new CampaignParticipant(entry.PersonId, entry.DisplayName, entry.WorkerType,
                entry.Department, true, "launch", launch.RosterSnapshotId, entry.DueOn, frozenFact));
        foreach (var ev in campaign.Amendments)
        {
            var amendments = await content.ReadJsonAsync<IReadOnlyList<CampaignAudienceAmendment>>(realm,
                ev.AmendmentsManifest, ct).ConfigureAwait(false);
            var fact = NativeFactReference.Of(campaign.Stream, ev);
            foreach (var amendment in amendments)
            {
                roster._amendments.Add(new CampaignAmendmentView(ev.Reconciliation, amendment.PersonId,
                    amendment.DisplayName, amendment.Reason, amendment.DueOn, ev.RosterSnapshotId,
                    ev.RosterContentSha256, ev.Actor, ev.AmendedAt));
                if (amendment.Reason is "joiner" or "mover_in")
                {
                    if (roster._participants.TryGetValue(amendment.PersonId, out var returning))
                    {
                        returning.RemovedReason = null;
                        returning.RemovedBy = null;
                        returning.Inclusion = amendment.Reason;
                        returning.IncludedBy = ev.RosterSnapshotId;
                        returning.DueOn = amendment.DueOn!.Value;
                        returning.Department = amendment.Department;
                        returning.WorkerType = amendment.WorkerType!;
                        returning.IncludingFact = fact;
                    }
                    else
                        roster.Add(new CampaignParticipant(amendment.PersonId, amendment.DisplayName,
                            amendment.WorkerType!, amendment.Department, false, amendment.Reason,
                            ev.RosterSnapshotId, amendment.DueOn!.Value, fact));
                }
                else if (roster._participants.TryGetValue(amendment.PersonId, out var leaving))
                {
                    leaving.RemovedReason = amendment.Reason;
                    leaving.RemovedBy = ev.RosterSnapshotId;
                }
            }
        }

        return roster;
    }

    void Add(CampaignParticipant participant)
    {
        _participants.Add(participant.PersonId, participant);
        _order.Add(participant);
    }
}
