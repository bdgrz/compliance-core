using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Reactors;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.Risks.Readers;

/// <summary>
///     Reads a risk's governance from its own owner streams and composes the public views. A risk's owner and triggers
///     come from its governance stream, its treatment assertion lines from the stream of each (risk, control), and its
///     actions from the stream of each action; every owner is discovered from its first fact and hydrated on its own, so
///     no program-wide ledger exists and nothing is retained as command state.
/// </summary>
public sealed class RiskGovernanceReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>Reads the exact physical/native cut of a compact governance owner, with no lifetime state on that owner.</summary>
    public async Task<IReadOnlyList<DomainEvent>> ReadGovernanceFactsAsync(RiskGovernance owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 && (owner.Latest is null ||
            ContentDigest.Sha256(facts[^1], facts[^1].GetType()) != ContentDigest.Sha256(owner.Latest, owner.Latest.GetType())))
            throw new ImmutableContentIntegrityException("The risk governance history differs from its exact hydrated cut.");
        for (var index = 0; index < facts.Count; index++)
        {
            var valid = facts[index] switch
            {
                RiskOwnerAssigned fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.RiskId == owner.RiskId && fact.Revision == index + 1,
                RiskReassessmentTriggered fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.RiskId == owner.RiskId && fact.Revision == index + 1,
                _ => false,
            };
            if (!valid)
                throw new ImmutableContentIntegrityException("The risk governance history contains another exact risk or predecessor.");
        }
        return facts;
    }

    /// <summary>Checks the semantic trigger identity in verified immutable history at a frozen command cut.</summary>
    public async Task<RiskReassessmentTriggerView?> FindTriggerAsync(RiskGovernance owner, Uuid triggerId,
        CancellationToken ct = default)
    {
        RiskReassessmentTriggerView? selected = null;
        var seen = new HashSet<Uuid>();
        foreach (var fact in (await ReadGovernanceFactsAsync(owner, ct).ConfigureAwait(false)).OfType<RiskReassessmentTriggered>())
        {
            var view = await ReadTriggerAsync(fact, ct).ConfigureAwait(false);
            if (!seen.Add(view.TriggerId))
                throw new ImmutableContentIntegrityException("A semantic reassessment trigger appears twice in the same risk history.");
            if (view.TriggerId == triggerId)
                selected = view;
        }
        return selected;
    }

    public async Task<RiskReassessmentTriggerView> ReadTriggerAsync(RiskReassessmentTriggered fact, CancellationToken ct = default)
    {
        var view = await FindingContent.ReadAsync<RiskReassessmentTriggerView>(content, fact.TenantId, fact.TriggerContent, ct).ConfigureAwait(false);
        var workload = view.TriggerKind == RiskStates.MethodChanged ? RiskMethodReassessmentReactor.WorkloadName : BoundaryRiskReassessmentReactor.WorkloadName;
        if (view.RiskId != fact.RiskId || view.TriggerKind is not (RiskStates.MethodChanged or RiskStates.BoundaryChanged) ||
            string.IsNullOrWhiteSpace(view.SourceReference) || view.SourceReference.Length > 4000 || view.RaisedAt == default || view.Status != "open" ||
            view.TriggerId != RiskGovernance.TriggerIdFor(fact.ProgramId, fact.RiskId, view.TriggerKind, view.SourceReference) ||
            fact.Operation.Actor.Kind != "system_process" || fact.Operation.Actor.Id != "reactor:" + workload)
            throw new ImmutableContentIntegrityException("The trigger body differs from its native risk identity or trusted producer.");
        return view;
    }

    public async Task<RiskOwnerView> ReadOwnerAsync(RiskOwnerAssigned fact, CancellationToken ct = default)
    {
        var view = await FindingContent.ReadAsync<RiskOwnerView>(content, fact.TenantId, fact.OwnerContent, ct).ConfigureAwait(false);
        if (view.PersonId == Uuid.Empty || view.CorrelatedMemberId == Uuid.Empty || RiskCommands.InvalidText(view.Rationale) ||
            view.Rationale != view.Rationale.Trim() || view.AssignedBy != fact.Operation.Actor || view.AssignedAt != fact.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The risk owner body differs from its original native attribution.");
        return view;
    }

    /// <summary>The governance of every risk of the program.</summary>
    public ValueTask<RiskGovernanceSnapshot> ReadAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default) =>
        CollectAsync(tenantId, programId, null, ct);

    /// <summary>The governance of one risk of the program.</summary>
    public ValueTask<RiskGovernanceSnapshot> ReadRiskAsync(Uuid tenantId, Uuid programId, Uuid riskId,
        CancellationToken ct = default) => CollectAsync(tenantId, programId, riskId, ct);

    /// <summary>Resolves an immutable treatment locator in its exact tenant/program/risk scope, retaining ambiguity.</summary>
    public async ValueTask<Uuid?> LocateControlAsync(Uuid tenantId, Uuid programId, Uuid riskId, Uuid treatmentId,
        CancellationToken ct = default)
    {
        var matches = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            RiskTreatmentAssertion.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not RiskControlTreatmentProposed proposed ||
                proposed.TenantId != tenantId || proposed.ProgramId != programId || proposed.RiskId != riskId ||
                proposed.Operation.LogicalId != treatmentId)
                continue;
            var view = await FindingContent.ReadAsync<RiskControlTreatmentView>(content, tenantId, proposed.TreatmentContent, ct).ConfigureAwait(false);
            if (view.RiskId != riskId || view.TreatmentId != treatmentId || view.ControlId == Uuid.Empty ||
                record.Stream != new RiskTreatmentAssertion(tenantId, programId, riskId, view.ControlId).Stream ||
                proposed.Operation.Owner != record.Stream || (proposed.Operation.FirstOwner ?? proposed.Operation.Owner) != record.Stream)
                throw new ImmutableContentIntegrityException("The immutable treatment locator differs from its exact native owner.");
            matches.Add(view.ControlId);
        }
        if (matches.Count > 1)
            throw new ImmutableContentIntegrityException("The scoped treatment identity is ambiguous across control assertion owners.");
        return matches.Count == 0 ? null : matches.Single();
    }

    /// <summary>Uses exact existing native admission facts to route an authorized retry before fresh source reads.</summary>
    internal async Task<EventStreamAddress?> LocateAssertionAdmissionAsync(Uuid tenantId, Uuid programId, Uuid riskId,
        Uuid commandId, CancellationToken ct = default)
    {
        var owners = new HashSet<EventStreamAddress>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            RiskTreatmentAssertion.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            var matches = record.Event switch
            {
                RiskControlTreatmentProposed fact => fact.TenantId == tenantId && fact.ProgramId == programId && fact.RiskId == riskId && fact.Operation.LogicalId == commandId,
                RiskControlTreatmentReviewed fact => fact.TenantId == tenantId && fact.ProgramId == programId && fact.RiskId == riskId && fact.Operation.LogicalId == commandId,
                RiskControlTreatmentRetired fact => fact.TenantId == tenantId && fact.ProgramId == programId && fact.RiskId == riskId && fact.Operation.LogicalId == commandId,
                _ => false,
            };
            if (!matches)
                continue;
            var operation = ((INativeOperationFact)record.Event).Operation;
            if (record.Stream.Realm != tenantId.ToString() || record.Stream.Area != RiskTreatmentAssertion.Area ||
                record.Stream.Resource != record.Event.Metadata.AggregateId.ToString() || operation.Owner != record.Stream ||
                (operation.FirstOwner ?? operation.Owner) != record.Stream || record.Event.Metadata.EventId == Uuid.Empty || record.Event.Metadata.AggregateVersion == 0)
                throw new ImmutableContentIntegrityException("The assertion admission locator has another exact native source binding.");
            owners.Add(record.Stream);
        }
        if (owners.Count > 1)
            throw new ImmutableContentIntegrityException("The scoped command identity is ambiguous across assertion owners.");
        return owners.Count == 0 ? null : owners.Single();
    }

    /// <summary>The treatments of a line composed from its facts, in the order proposed.</summary>
    public async ValueTask<IReadOnlyList<RiskControlTreatmentView>> ComposeTreatmentsAsync(Uuid tenantId,
        RiskTreatmentAssertion line, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        var views = new List<RiskControlTreatmentView>();
        var facts = await RiskTreatmentAssertionHistory.ReadAsync(events, line, ct).ConfigureAwait(false);
        var composed = RiskTreatmentAssertionRules.FromFacts(tenantId, line.ProgramId, line.RiskId, line.ControlId, facts);
        foreach (var treatment in composed.Treatments)
        {
            var proposed = await FindingContent.ReadAsync<RiskControlTreatmentView>(content, tenantId,
                treatment.Proposed.TreatmentContent, ct).ConfigureAwait(false);
            if (proposed.TreatmentId != treatment.TreatmentId || proposed.RiskId != line.RiskId ||
                proposed.ControlId != line.ControlId || proposed.ControlVersionId == Uuid.Empty ||
                proposed.ProposedBy != treatment.Proposed.Operation.Actor || proposed.ProposedAt != treatment.Proposed.Operation.AcceptedAt ||
                proposed.Status != RiskStates.TreatmentProposed || RiskCommands.InvalidText(proposed.Rationale) || proposed.Rationale != proposed.Rationale.Trim())
                throw new ImmutableContentIntegrityException("The immutable treatment differs from its exact source line, proposal or original author.");
            var view = proposed with { Status = treatment.Status };
            if (treatment.Reviewed is { } reviewed)
                view = view with
                {
                    ReviewDecisionId = reviewed.DecisionId,
                    ReviewedBy = reviewed.Reviewer,
                    ReviewRationale = reviewed.Rationale,
                    ReviewedAt = reviewed.ReviewedAt,
                    SeparationOfDutiesWaiverId = reviewed.SeparationOfDutiesWaiverId,
                };
            if (treatment.Retired is { } retired)
                view = view with
                {
                    RetiredBy = retired.RetiredBy,
                    RetirementRationale = retired.Rationale,
                    RetiredAt = retired.RetiredAt,
                };
            views.Add(view with { Revision = line.Revision });
        }

        return views;
    }

    /// <summary>The action composed from its facts, or null when it was not added.</summary>
    public async ValueTask<RiskTreatmentActionView?> ComposeActionAsync(Uuid tenantId, RiskTreatmentAction action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (tenantId != action.TenantId)
            throw new ImmutableContentIntegrityException("The action composition belongs to another tenant.");
        if (await RiskTreatmentActionHistory.ReadInitialAsync(content, action, ct).ConfigureAwait(false) is not { } view)
            return null;
        var completions = new List<RiskTreatmentActionCompletionView>();
        foreach (var fact in await RiskTreatmentActionHistory.ReadAsync(events, action, ct).ConfigureAwait(false))
        {
            switch (fact)
            {
                case RiskTreatmentActionRevised revised:
                    view = view with
                    {
                        Title = revised.Title,
                        TargetState = revised.TargetState,
                        ExpectedEvidence = revised.ExpectedEvidence,
                        DueOn = revised.DueOn,
                        AccountableMemberId = revised.AccountableMemberId,
                        EvidenceRequestIds = await FindingContent.ReadAsync<IReadOnlyList<Uuid>>(content, tenantId,
                            revised.EvidenceRequestIdsManifest, ct).ConfigureAwait(false),
                    };
                    break;
                case RiskTreatmentActionCompletionSubmitted submitted:
                    completions.Add(new RiskTreatmentActionCompletionView(submitted.SubmissionId, submitted.Summary,
                        await FindingContent.ReadAsync<IReadOnlyList<Uuid>>(content, tenantId,
                            submitted.EvidenceRequestIdsManifest, ct).ConfigureAwait(false), submitted.SubmittedBy,
                        submitted.SubmittedAt, null, null, null, null, null, null));
                    break;
                case RiskTreatmentActionCompletionReviewed reviewed:
                    completions[^1] = completions[^1] with
                    {
                        ReviewDecisionId = reviewed.DecisionId,
                        ReviewOutcome = reviewed.Outcome,
                        ReviewedBy = reviewed.Reviewer,
                        ReviewRationale = reviewed.Rationale,
                        ReviewedAt = reviewed.ReviewedAt,
                        SeparationOfDutiesWaiverId = reviewed.SeparationOfDutiesWaiverId,
                    };
                    break;
                case RiskTreatmentActionCancelled cancelled:
                    view = view with
                    {
                        CancellationRationale = cancelled.Rationale,
                        CancelledBy = cancelled.CancelledBy,
                        CancelledAt = cancelled.CancelledAt,
                    };
                    break;
            }
        }

        return view with { Status = action.Status, Completions = completions, Revision = action.Revision };
    }

    /// <summary>The submitting members of the action's completion submissions, by submission id.</summary>
    public async Task<IReadOnlyDictionary<Uuid, Uuid>> SubmittersOfAsync(RiskTreatmentAction action,
        CancellationToken ct = default) =>
        (await RiskTreatmentActionHistory.ReadAsync(events, action, ct).ConfigureAwait(false))
        .OfType<RiskTreatmentActionCompletionSubmitted>().ToDictionary(static submission => submission.SubmissionId,
            static submission => submission.SubmitterMemberId);

    /// <summary>
    ///     Discovers every governed risk, every (risk, control) assertion line and every action of the program from the
    ///     first facts of their own owner streams.
    /// </summary>
    public async ValueTask<RiskOwnerDiscovery> DiscoverAsync(Uuid tenantId, Uuid programId, Uuid? only,
        CancellationToken ct = default)
    {
        var governed = new SortedSet<Uuid>(UuidOrder);
        var lines = new SortedSet<(Uuid Risk, Uuid Control)>(Comparer<(Uuid Risk, Uuid Control)>.Create(
            static (left, right) => string.CompareOrdinal($"{left.Risk}|{left.Control}", $"{right.Risk}|{right.Control}")));
        var actions = new SortedSet<(Uuid Risk, Uuid Action)>(Comparer<(Uuid Risk, Uuid Action)>.Create(
            static (left, right) => string.CompareOrdinal($"{left.Risk}|{left.Action}", $"{right.Risk}|{right.Action}")));
        var realm = tenantId.ToString();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(realm, RiskGovernance.Area),
                           EventCursor.Start, ct).ConfigureAwait(false))
            switch (record.Event)
            {
                case RiskOwnerAssigned ev when !record.Event.Metadata.IsAudit && Wanted(ev.TenantId, ev.ProgramId, ev.RiskId):
                    governed.Add(ev.RiskId);
                    break;
                case RiskReassessmentTriggered ev when !record.Event.Metadata.IsAudit && Wanted(ev.TenantId, ev.ProgramId, ev.RiskId):
                    governed.Add(ev.RiskId);
                    break;
            }

        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(realm,
                           RiskTreatmentAssertion.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is RiskControlTreatmentProposed proposed &&
                Wanted(proposed.TenantId, proposed.ProgramId, proposed.RiskId))
                lines.Add((proposed.RiskId, (await FindingContent.ReadAsync<RiskControlTreatmentView>(content,
                    tenantId, proposed.TreatmentContent, ct).ConfigureAwait(false)).ControlId));

        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(realm, RiskTreatmentAction.Area),
                           EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is RiskTreatmentActionAdded added &&
                Wanted(added.TenantId, added.ProgramId, added.RiskId))
                actions.Add((added.RiskId, added.Operation.LogicalId));

        return new RiskOwnerDiscovery(governed, lines, actions);

        bool Wanted(Uuid eventTenant, Uuid eventProgram, Uuid eventRisk) =>
            eventTenant == tenantId && eventProgram == programId && (only is null || only == eventRisk);
    }

    async ValueTask<RiskGovernanceSnapshot> CollectAsync(Uuid tenantId, Uuid programId, Uuid? only, CancellationToken ct)
    {
        var (governed, lines, actions) = await DiscoverAsync(tenantId, programId, only, ct).ConfigureAwait(false);
        var risks = new SortedSet<Uuid>(governed, UuidOrder);
        risks.UnionWith(lines.Select(static line => line.Risk));
        risks.UnionWith(actions.Select(static action => action.Risk));
        var records = new Dictionary<Uuid, RiskGovernanceRecord>();
        foreach (var riskId in risks)
        {
            var governance = await reader.HydrateAsync(new RiskGovernance(tenantId, programId, riskId), ct)
                .ConfigureAwait(false);
            var owner = governance.Owner is { } assigned
                ? await ReadOwnerAsync(assigned, ct).ConfigureAwait(false)
                : (RiskOwnerView?)null;
            var triggers = new List<RiskReassessmentTriggerView>();
            var triggerIds = new HashSet<Uuid>();
            foreach (var trigger in (await ReadGovernanceFactsAsync(governance, ct).ConfigureAwait(false)).OfType<RiskReassessmentTriggered>())
            {
                var view = await ReadTriggerAsync(trigger, ct).ConfigureAwait(false);
                if (!triggerIds.Add(view.TriggerId))
                    throw new ImmutableContentIntegrityException("A risk history contains duplicate semantic triggers.");
                triggers.Add(view);
            }
            var treatments = new List<RiskControlTreatmentView>();
            foreach (var (_, controlId) in lines.Where(line => line.Risk == riskId))
            {
                var line = await reader.HydrateAsync(new RiskTreatmentAssertion(tenantId, programId, riskId, controlId),
                    ct).ConfigureAwait(false);
                treatments.AddRange(await ComposeTreatmentsAsync(tenantId, line, ct).ConfigureAwait(false));
            }

            var views = new List<RiskTreatmentActionView>();
            var submitters = new Dictionary<(Uuid ActionId, Uuid SubmissionId), Uuid>();
            foreach (var (_, actionId) in actions.Where(action => action.Risk == riskId))
            {
                var action = await reader.HydrateAsync(new RiskTreatmentAction(tenantId, programId, riskId, actionId),
                    ct).ConfigureAwait(false);
                if (await ComposeActionAsync(tenantId, action, ct).ConfigureAwait(false) is not { } view)
                    continue;
                views.Add(view);
                foreach (var (submission, submitter) in await SubmittersOfAsync(action, ct).ConfigureAwait(false))
                    submitters[(action.ActionId, submission)] = submitter;
            }

            records.Add(riskId, new RiskGovernanceRecord(governance.Revision, owner, treatments.OrderBy(
                static treatment => treatment.ProposedAt).ToArray(), triggers, views.OrderBy(static action => action.CreatedAt)
                .ToArray(), submitters));
        }

        return new RiskGovernanceSnapshot(tenantId, programId, records);
    }

    /// <summary>The owner streams of a program's risk governance, discovered from their first facts.</summary>
    public sealed record RiskOwnerDiscovery(IReadOnlyCollection<Uuid> Governed,
        IReadOnlyCollection<(Uuid Risk, Uuid Control)> Lines, IReadOnlyCollection<(Uuid Risk, Uuid Action)> Actions);

    static readonly Comparer<Uuid> UuidOrder = Comparer<Uuid>.Create(static (left, right) =>
        string.CompareOrdinal(left.ToString(), right.ToString()));
}
