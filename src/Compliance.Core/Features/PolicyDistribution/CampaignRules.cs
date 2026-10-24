using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The business rules of a campaign: what a launch freezes, how a reconciliation amends the audience, and what
///     each participant result must satisfy before the campaign admits it. Every rule judges the campaign's own facts
///     and the admitted results at the revision the caller expected, never a projection.
/// </summary>
static class CampaignRules
{
    public const int MaximumInstructions = 4000;

    public static CommandFailure? CheckLaunch(DateOnly dueOn, DateOnly launchedOn, string? instructions)
    {
        if (dueOn < launchedOn)
            return CommandFailure.InvalidContent("A campaign due date cannot be before its launch.");
        return instructions?.Length > MaximumInstructions
            ? CommandFailure.InvalidContent("Campaign instructions must be at most 4000 characters.")
            : null;
    }

    /// <summary>The frozen launch audience, one entry per matching person in canonical person order.</summary>
    public static IReadOnlyList<CampaignAudienceEntry> Entries(RosterAudience audience, DateOnly dueOn)
    {
        ArgumentNullException.ThrowIfNull(audience);
        return audience.Matching.Values
            .OrderBy(static match => match.PersonId.ToString(), StringComparer.Ordinal)
            .Select(match => new CampaignAudienceEntry(match.PersonId, match.DisplayName, match.WorkerType,
                match.Department, match.StartDate, Max(dueOn, match.StartDate.AddDays(
                    PolicyDistributionCampaign.JoinerDays))))
            .ToArray();
    }

    /// <summary>
    ///     The attributed amendments of a reconciliation: removed people keep their history, additions are joiners
    ///     or movers with a due date. The frozen launch audience is never edited.
    /// </summary>
    public static IReadOnlyList<CampaignAudienceAmendment> Amendments(CampaignRoster roster, RosterAudience current,
        IReadOnlySet<Uuid> previouslyPresent, DateOnly launchDueOn, DateOnly observedOn)
    {
        var amendments = new List<CampaignAudienceAmendment>();
        foreach (var participant in roster.Order.Where(static participant => participant.IsActive))
            if (!current.Matching.ContainsKey(participant.PersonId))
                amendments.Add(new CampaignAudienceAmendment(participant.PersonId, participant.DisplayName,
                    current.Present.Contains(participant.PersonId) ? "mover_out" : "leaver", null, null, null));
        foreach (var match in current.Matching.Values.OrderBy(static match => match.PersonId.ToString(),
                     StringComparer.Ordinal))
        {
            if (roster.FindActive(match.PersonId) is not null)
                continue;
            var joiner = !previouslyPresent.Contains(match.PersonId);
            amendments.Add(new CampaignAudienceAmendment(match.PersonId, match.DisplayName,
                joiner ? "joiner" : "mover_in", match.WorkerType, match.Department,
                joiner
                    ? match.StartDate.AddDays(PolicyDistributionCampaign.JoinerDays)
                    : Max(launchDueOn, observedOn.AddDays(PolicyDistributionCampaign.JoinerDays))));
        }

        return amendments;
    }

    /// <summary>The checks every participant result shares: an open campaign and a currently included person.</summary>
    public static CommandFailure? CheckOpen(PolicyDistributionCampaign campaign)
    {
        if (!campaign.IsLaunched)
            return CommandFailure.MissingRecord("The campaign was not found.");
        if (campaign.IsClosed)
            return CommandFailure.StateConflict("The campaign is closed.");
        return campaign.IsSealed
            ? CommandFailure.StateConflict("The campaign is closing; its result admission is sealed.")
            : null;
    }

    public static CommandFailure? CheckAcknowledgement(CampaignState state, AcknowledgePolicy request)
    {
        var campaign = state.Campaign;
        if (CheckOpen(campaign) is { } failure)
            return failure;
        var subject = campaign.Subject!;
        if (subject.Kind != "policy")
            return CommandFailure.StateConflict("A training campaign records completions, not acknowledgements.");
        if (state.Roster.FindActive(request.PersonId) is null)
            return CommandFailure.MissingRecord("The person is not in this campaign's current audience.");
        if (request.PolicyVersion != subject.Version ||
            !StringComparer.Ordinal.Equals(request.ContentSha256, subject.ContentSha256))
            return CommandFailure.StateConflict(
                "The acknowledgement must name this campaign's exact policy version and content hash.");
        if (!StringComparer.Ordinal.Equals(request.AcknowledgementText, campaign.Launch!.AcknowledgementText))
            return CommandFailure.InvalidContent("The acknowledgement must store the exact acknowledgement text shown.");
        return Admitted(state, request.PersonId, CampaignParticipantResultKind.PolicyAcknowledgement).Any()
            ? CommandFailure.StateConflict("The person already acknowledged this campaign.")
            : null;
    }

    public static CommandFailure? CheckCompletion(CampaignState state, RecordTrainingCompletion request,
        DateTimeOffset now)
    {
        var campaign = state.Campaign;
        if (CheckOpen(campaign) is { } failure)
            return failure;
        var subject = campaign.Subject!;
        if (subject.Kind != "training")
            return CommandFailure.StateConflict("A policy campaign records acknowledgements, not completions.");
        if (state.Roster.FindActive(request.PersonId) is null)
            return CommandFailure.MissingRecord("The person is not in this campaign's current audience.");
        if (request.RequirementVersion != subject.Version)
            return CommandFailure.StateConflict(
                "The completion must name this campaign's exact training requirement version.");
        if (request.Source is not ("manual" or "lms_export") ||
            string.IsNullOrWhiteSpace(request.EvidenceReference) || request.EvidenceReference.Length > 1024)
            return CommandFailure.InvalidContent(
                "A completion requires a manual or lms_export source and an evidence reference of at most 1024 characters.");
        if (request.CompletedOn > DateOnly.FromDateTime(now.UtcDateTime))
            return CommandFailure.InvalidContent("A completion cannot be dated in the future.");
        return Admitted(state, request.PersonId, CampaignParticipantResultKind.TrainingCompletion).Any()
            ? CommandFailure.StateConflict("A completion is already recorded for this person.")
            : null;
    }

    public static async ValueTask<CommandFailure?> CheckWaiverAsync(IImmutableContentStore content, CampaignState state,
        ApproveCampaignWaiver request, DateTimeOffset now, CancellationToken ct)
    {
        var campaign = state.Campaign;
        if (CheckOpen(campaign) is { } failure)
            return failure;
        if (state.Roster.FindActive(request.PersonId) is null)
            return CommandFailure.MissingRecord("The person is not in this campaign's current audience.");
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (Admitted(state, request.PersonId, CampaignParticipantResultKind.Waiver).LastOrDefault() is { } last)
        {
            var prior = await CampaignAdmissions.ReadAuthoredAsync<ApproveCampaignWaiver>(content,
                campaign.TenantId, last, ct).ConfigureAwait(false);
            if (prior.ExpiresOn > today)
                return CommandFailure.StateConflict("The person already has an active exception.");
        }

        if (Admitted(state, request.PersonId, CampaignParticipantResultKind.PolicyAcknowledgement).Any() ||
            Admitted(state, request.PersonId, CampaignParticipantResultKind.TrainingCompletion).Any())
            return CommandFailure.StateConflict("A satisfied person needs no exception.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 4000)
            return CommandFailure.InvalidContent("An exception requires a reason of at most 4000 characters.");
        return request.ExpiresOn <= today || request.ExpiresOn > today.AddMonths(12)
            ? CommandFailure.InvalidContent("An exception must expire within 12 months.")
            : null;
    }

    public static CommandFailure? CheckClosure(CampaignState state, CloseCampaign request)
    {
        if (CheckOpen(state.Campaign) is { } failure)
            return failure;
        return string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Length > 4000
            ? CommandFailure.InvalidContent("Closing a campaign requires a rationale of at most 4000 characters.")
            : null;
    }

    /// <summary>The results of one kind that the campaign admitted for a person, in admission order.</summary>
    public static IEnumerable<PolicyParticipantResultAdmitted> Admitted(CampaignState state, Uuid personId,
        CampaignParticipantResultKind kind) =>
        state.Campaign.AdmittedFor(PolicyCampaignParticipant.StreamOf(state.Campaign.TenantId, state.Campaign.Id,
                personId))
            .Where(admitted => PolicyDistributionCampaign.KindOf(admitted) == kind);

    static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;
}
