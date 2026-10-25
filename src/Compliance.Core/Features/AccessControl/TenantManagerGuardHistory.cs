using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Proves every guard transition and affected after-authority against full transient rules and the pinned baseline.</summary>
public static class TenantManagerGuardHistory
{
    public static async Task<TenantManagerGuardHistorySnapshot> ReadAsync(IDomainEventReader events, TenantManagerGuard owner,
        GuardBaseline baseline, CancellationToken ct = default)
    {
        if (baseline.Contributions is null || baseline.Contributions.Select(value => value.MemberEpisodeId).Distinct().Count() != baseline.Contributions.Count ||
            baseline.Contributions.Any(value => value.MemberId == Uuid.Empty || value.MemberEpisodeId == Uuid.Empty || value.AssignmentGeneration <= 0 ||
                value.AssignmentId != RbacIds.TeamMember(owner.TenantId, BuiltInRbac.AdministratorsTeamId(owner.TenantId), value.MemberId) ||
                value.Reasons is null || value.Reasons.Distinct(StringComparer.Ordinal).Count() != value.Reasons.Count ||
                value.Reasons.Any(reason => reason is not (TenantManagerGuard.Suspended or TenantManagerGuard.Deprovisioned or TenantManagerGuard.RemovedFromAdministrators))))
            throw Invalid("The pinned baseline must preserve exact distinct member/episode/assignment identities and known denial reasons.");
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var model = new TenantManagerGuardRules(owner.TenantId, baseline);
        var logical = new HashSet<Uuid>();
        var roots = new Dictionary<Uuid, (OperationProvenance Operation, ImmutableContentRef Intent)>();
        var digest = NativeReplayCommitment.Empty;
        var metadata = NativeReplayCommitment.Empty;
        var known = NativeReplayCommitment.Empty;
        TenantManagerRestored? paired = null;
        foreach (var fact in facts)
        {
            if (fact is not ITenantManagerAuthorityFact native || native.Operation.Owner != owner.Stream ||
                native.CurrentAuthority is not { } authority || authority.MemberId == Uuid.Empty ||
                !Uuid.TryParse(native.Operation.Episode, System.Globalization.CultureInfo.InvariantCulture, out var episode) ||
                episode != authority.EpisodeId || (native.Operation.FirstOwner ?? native.Operation.Owner) != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || native.Operation.AcceptedAt == default ||
                native.Operation.AuthoredIntentSha256 is not { Length: 64 } || !native.Operation.AuthoredIntentSha256.All(Uri.IsHexDigit))
                throw Invalid("A guard fact differs from its exact tenant, membership episode or first owner.");
            if (paired is not null && (fact is not TenantManagerRestorationCompleted completed ||
                completed.Operation != paired.Operation || completed.Intent != paired.Intent || completed.OperationResult != paired.OperationResult))
                throw Invalid("Restored availability must be paired atomically with its exact matching completion.");
            if (fact is ITenantManagerGuardFact typed)
            {
                if (typed.Generation != (long)model.Version + 1 || !ValidSource(owner.TenantId, authority.MemberId, typed.Owner))
                    throw Invalid("A guard generation/source owner differs from its actual native sequence and exact member.");
                if (fact is TenantManagerWithdrawalAccepted or TenantManagerRestorationAccepted or TenantManagerSourceMutationAdmitted)
                {
                    if (native.Operation.RootOperationId != native.Operation.LogicalId || native.Operation.CausationEventId is not null ||
                        native.Operation.AffectedOwner != typed.Owner || native.Operation.ExpectedRevisionOwner != typed.Owner || native.FirstResponse is null ||
                        !roots.TryAdd(native.Operation.RootOperationId, (native.Operation, typed.Intent)))
                        throw Invalid("A guard admission is its own exact original source-bound root once.");
                    var plan = model.Plan(native.Operation.CommandKind, episode);
                    if (!plan.IsSuccess || fact is TenantManagerWithdrawalAccepted && !plan.Value!.IsWithdrawal ||
                        fact is TenantManagerRestorationAccepted && !plan.Value!.IsRestoration ||
                        fact is TenantManagerSourceMutationAdmitted && (plan.Value!.IsWithdrawal || plan.Value.IsRestoration))
                        throw Invalid("The historical admission violates last-administrator or exact episode transition rules.");
                }
                else if (!roots.TryGetValue(native.Operation.RootOperationId, out var root) || native.Operation.CausationEventId is null ||
                    native.Operation.Actor != root.Operation.Actor || native.Operation.AuthoredIntentSha256 != root.Operation.AuthoredIntentSha256 || native.Operation.Episode != root.Operation.Episode || typed.Intent != root.Intent ||
                    !model.IsPending(native.Operation.RootOperationId) ||
                    fact is TenantManagerWithdrawalCompleted && model.Pending.GetValueOrDefault(native.Operation.RootOperationId)?.IsWithdrawal != true ||
                    fact is TenantManagerRestorationCompleted && model.Pending.GetValueOrDefault(native.Operation.RootOperationId)?.IsWithdrawal != false)
                    throw Invalid("A guard completion differs from its original admitted root and live transition.");
            }
            if (fact is TenantManagerRestored restored)
            {
                if (restored.TenantId != owner.TenantId || restored.MemberId != authority.MemberId ||
                    !model.Pending.TryGetValue(native.Operation.RootOperationId, out var pending) || pending.IsWithdrawal ||
                    model.ReasonsFor(episode).Any(reason => reason != pending.Reason))
                    throw Invalid("Availability can only return for the matching live restoration with no remaining denial reason.");
                paired = restored;
            }
            else
            {
                if (!logical.Add(native.Operation.LogicalId))
                    throw Invalid("The guard history repeats an unselected logical receipt.");
                paired = null;
                if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                    (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                    throw Invalid("The selected guard receipt differs from its exact payload and full metadata.");
            }
            try
            {
                model.ObserveFact(fact);
            }
            catch (InvalidOperationException error)
            {
                throw Invalid($"The complete guard chronology cannot be verified: {error.Message}");
            }
            if (!Same(authority, model.Current(episode)))
                throw Invalid("The guard after-authority differs from the complete attributable episode history.");
            digest = NativeReplayCommitment.Append(digest, fact);
            metadata = NativeReplayCommitment.Append(metadata, fact, true);
            if (fact.Metadata.AggregateVersion <= owner.ReplayCut.KnownThroughVersion)
                known = NativeReplayCommitment.Append(known, fact, true);
        }
        if (paired is not null)
            throw new ImmutableContentUnavailableException("The matching atomic restoration completion is not visible; availability remains unqualified.");
        var expected = model.Authority.Where(value => value.Available || value.EpisodeId == owner.SelectedEpisode ||
            model.Pending.Values.Any(pending => pending.Episode == value.EpisodeId)).ToDictionary(value => value.EpisodeId);
        if (expected.Count != owner.Authority.Count || expected.Any(pair => !owner.Authority.TryGetValue(pair.Key, out var actual) || !Same(pair.Value, actual)) ||
            model.Pending.Count != owner.Pending.Count || model.Pending.Any(pair => owner.Pending.GetValueOrDefault(pair.Key) != pair.Value) ||
            model.AvailableCount != owner.AvailableCount || model.BaselineRef != owner.BaselineRef)
            throw Invalid("The exact available/live/selected identities differ from full guard history.");
        owner.ReplayCut.Confirm(owner, digest, metadata, known);
        return new TenantManagerGuardHistorySnapshot(model.Authority.ToDictionary(value => value.EpisodeId), model.Pending, facts);
    }
    internal static bool Same(TenantManagerEpisodeAuthority a, TenantManagerEpisodeAuthority b) =>
        a.EpisodeId == b.EpisodeId && a.MemberId == b.MemberId && a.HasContribution == b.HasContribution && a.IsRegistered == b.IsRegistered &&
        a.AssignmentGeneration == b.AssignmentGeneration && a.Available == b.Available && a.Reasons.SequenceEqual(b.Reasons) && a.NeutralReasons.SequenceEqual(b.NeutralReasons);
    static bool ValidSource(Uuid tenant, Uuid member, EventStreamAddress source) => source.Realm == tenant.ToString() && (source.Area switch
    {
        "rbac-members" => source.Resource == member.ToString(),
        "rbac-team-members" => source.Resource == RbacIds.TeamMember(tenant, BuiltInRbac.AdministratorsTeamId(tenant), member).ToString(),
        _ => false,
    });
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
