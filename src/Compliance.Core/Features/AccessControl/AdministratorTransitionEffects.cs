using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Builds the typed receiver commands of the administrator transition process. Every command id is the
///     deterministic step id the process driver derived from the root source fact, the receiver, the target and the
///     member episode through <c>EffectIdentity</c>, so a replayed step sends the identical command. Proofs are built
///     from the committed facts the process record references, never from caller input.
/// </summary>
static class AdministratorTransitionEffects
{
    /// <summary>The apply receiver the transition kind selects; exactly one per guard fact.</summary>
    public static string ApplyReceiver(AdministratorTransitionKind kind) => kind switch
    {
        AdministratorTransitionKind.SuspendMember or AdministratorTransitionKind.DeprovisionMember =>
            AdministratorReceivers.ApplyMemberWithdrawal,
        AdministratorTransitionKind.RemoveAdministratorsAssignment => AdministratorReceivers.ApplyAdministratorAssignmentWithdrawal,
        AdministratorTransitionKind.ReinstateMember => AdministratorReceivers.ApplyMemberRestoration,
        AdministratorTransitionKind.RestoreAdministratorsAssignment or
            AdministratorTransitionKind.RegisterAdministratorsAssignment => AdministratorReceivers.ApplyAdministratorAssignmentRestoration,
        AdministratorTransitionKind.NeutralMemberMutation => AdministratorReceivers.ApplyNeutralMemberSourceMutation,
        AdministratorTransitionKind.NeutralAssignmentMutation => AdministratorReceivers.ApplyNeutralAdministratorAssignmentMutation,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The completion receiver the transition kind selects, or null for the neutral kinds.</summary>
    public static string? CompleteReceiver(AdministratorTransitionKind kind) => kind switch
    {
        AdministratorTransitionKind.SuspendMember or AdministratorTransitionKind.DeprovisionMember or
            AdministratorTransitionKind.RemoveAdministratorsAssignment => AdministratorReceivers.CompleteAdministratorWithdrawal,
        AdministratorTransitionKind.ReinstateMember or AdministratorTransitionKind.RestoreAdministratorsAssignment or
            AdministratorTransitionKind.RegisterAdministratorsAssignment => AdministratorReceivers.CompleteAdministratorRestoration,
        _ => null,
    };

    /// <summary>The stable wire name of a transition kind, as carried by the withdrawal receiver.</summary>
    public static string WireName(AdministratorTransitionKind kind) => kind switch
    {
        AdministratorTransitionKind.SuspendMember => "suspend_member",
        AdministratorTransitionKind.DeprovisionMember => "deprovision_member",
        AdministratorTransitionKind.RemoveAdministratorsAssignment => "remove_administrators_assignment",
        AdministratorTransitionKind.ReinstateMember => "reinstate_member",
        AdministratorTransitionKind.RestoreAdministratorsAssignment => "restore_administrators_assignment",
        AdministratorTransitionKind.RegisterAdministratorsAssignment => "register_administrators_assignment",
        AdministratorTransitionKind.NeutralMemberMutation => "neutral_member_mutation",
        AdministratorTransitionKind.NeutralAssignmentMutation => "neutral_assignment_mutation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Builds the receiver command of the step, or null when the transition kind did not select it.</summary>
    public static async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step,
        AdministratorTransitionIntent intent, ISourceFactResolver resolver, IImmutableContentStore content,
        CancellationToken ct)
    {
        var kind = intent.TransitionKind;
        var realm = step.Record.Key.Realm;
        if (step.Receiver != ApplyReceiver(kind) && step.Receiver != CompleteReceiver(kind))
            return null;
        switch (step.Receiver)
        {
            case AdministratorReceivers.ApplyMemberWithdrawal:
                {
                    var (proof, fact, _) = await SourceFactProofs.BuildAsync<TenantManagerWithdrawalAccepted>(resolver,
                        content, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ApplyMemberWithdrawal(step.CommandId, proof, fact.Owner, fact.Operation.RootOperationId,
                        fact.Generation, (long)intent.MemberRevision, step.Record.Intent, intent.MemberId,
                        intent.MemberEpisodeId, WireName(kind));
                }
            case AdministratorReceivers.ApplyAdministratorAssignmentWithdrawal:
                {
                    var (proof, fact, _) = await SourceFactProofs.BuildAsync<TenantManagerWithdrawalAccepted>(resolver,
                        content, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ApplyAdministratorAssignmentWithdrawal(step.CommandId, proof, fact.Owner,
                        fact.Operation.RootOperationId, fact.Generation, (long?)intent.ExpectedAssignmentRevision,
                        step.Record.Intent, intent.MemberId, intent.MemberEpisodeId, intent.AssignmentId!.Value);
                }
            case AdministratorReceivers.ApplyMemberRestoration:
                {
                    var (proof, fact, _) = await SourceFactProofs.BuildAsync<TenantManagerRestorationAccepted>(resolver,
                        content, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ApplyMemberRestoration(step.CommandId, proof, fact.Owner, fact.Operation.RootOperationId,
                        fact.Generation, null, step.Record.Intent, intent.MemberId, intent.MemberEpisodeId);
                }
            case AdministratorReceivers.ApplyAdministratorAssignmentRestoration:
                {
                    var (proof, fact, _) = await SourceFactProofs.BuildAsync<TenantManagerRestorationAccepted>(resolver,
                        content, realm, step.Record.Source, ct).ConfigureAwait(false);
                    var revision = intent.ExpectedAssignmentRevision ?? 0;
                    return new ApplyAdministratorAssignmentRestoration(step.CommandId, proof, fact.Owner,
                        fact.Operation.RootOperationId, fact.Generation, (long)revision, step.Record.Intent,
                        intent.MemberId, intent.MemberEpisodeId, intent.AssignmentId!.Value, revision);
                }
            case AdministratorReceivers.ApplyNeutralMemberSourceMutation:
                {
                    var (proof, fact, _) = await SourceFactProofs.BuildAsync<TenantManagerSourceMutationAdmitted>(resolver,
                        content, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ApplyNeutralMemberSourceMutation(step.CommandId, proof, fact.Owner,
                        fact.Operation.RootOperationId, fact.Generation, (long)intent.MemberRevision, step.Record.Intent,
                        intent.MemberId, intent.MemberEpisodeId, MutationKind(fact.Operation.CommandKind));
                }
            case AdministratorReceivers.ApplyNeutralAdministratorAssignmentMutation:
                {
                    var (proof, fact, _) = await SourceFactProofs.BuildAsync<TenantManagerSourceMutationAdmitted>(resolver,
                        content, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new ApplyNeutralAdministratorAssignmentMutation(step.CommandId, proof, fact.Owner,
                        fact.Operation.RootOperationId, fact.Generation, (long?)intent.ExpectedAssignmentRevision,
                        step.Record.Intent, intent.MemberId, intent.MemberEpisodeId, intent.AssignmentId!.Value);
                }
            case AdministratorReceivers.CompleteAdministratorWithdrawal:
                return await CompleteWithdrawalAsync(step, intent, resolver, content, ct).ConfigureAwait(false);
            case AdministratorReceivers.CompleteAdministratorRestoration:
                return await CompleteRestorationAsync(step, intent, resolver, content, ct).ConfigureAwait(false);
            default:
                return null;
        }
    }

    static async Task<CompleteAdministratorWithdrawal> CompleteWithdrawalAsync(ProcessStep step,
        AdministratorTransitionIntent intent, ISourceFactResolver resolver, IImmutableContentStore content,
        CancellationToken ct)
    {
        var realm = step.Record.Key.Realm;
        var applied = Applied(step, intent.TransitionKind);
        var root = await RootAsync(step, resolver, ct).ConfigureAwait(false);
        CompleteAdministratorWithdrawalSourceProof source = intent.TransitionKind switch
        {
            AdministratorTransitionKind.SuspendMember => new CompleteAdministratorWithdrawalSourceProof.SuspendedProof(
                (await SourceFactProofs.BuildAsync<MemberSuspended>(resolver, content, realm, applied, ct)
                    .ConfigureAwait(false)).Proof),
            AdministratorTransitionKind.DeprovisionMember => new CompleteAdministratorWithdrawalSourceProof.DeprovisionedProof(
                (await SourceFactProofs.BuildAsync<MemberDeprovisioned>(resolver, content, realm, applied, ct)
                    .ConfigureAwait(false)).Proof),
            _ => new CompleteAdministratorWithdrawalSourceProof.RemovedProof(
                (await SourceFactProofs.BuildAsync<TeamMemberRemoved>(resolver, content, realm, applied, ct)
                    .ConfigureAwait(false)).Proof),
        };
        return new CompleteAdministratorWithdrawal(step.CommandId, source, step.Record.Source.Stream,
            root.Operation.RootOperationId, root.Generation, null, step.Record.Intent, intent.MemberId,
            intent.MemberEpisodeId);
    }

    static async Task<CompleteAdministratorRestoration> CompleteRestorationAsync(ProcessStep step,
        AdministratorTransitionIntent intent, ISourceFactResolver resolver, IImmutableContentStore content,
        CancellationToken ct)
    {
        var realm = step.Record.Key.Realm;
        var applied = Applied(step, intent.TransitionKind);
        var root = await RootAsync(step, resolver, ct).ConfigureAwait(false);
        SourceFactLocator locator;
        CompleteAdministratorRestorationSourceProof source;
        if (intent.TransitionKind == AdministratorTransitionKind.ReinstateMember)
        {
            var built = await SourceFactProofs.BuildAsync<MemberReinstated>(resolver, content, realm, applied, ct)
                .ConfigureAwait(false);
            source = new CompleteAdministratorRestorationSourceProof.ReinstatedProof(built.Proof);
            locator = built.Locator;
        }
        else
        {
            var built = await SourceFactProofs.BuildAsync<TeamMemberAssigned>(resolver, content, realm, applied, ct)
                .ConfigureAwait(false);
            source = new CompleteAdministratorRestorationSourceProof.AssignedProof(built.Proof);
            locator = built.Locator;
        }

        var proofs = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(step.CommandId, AdministratorEligibilityProofs.Schema),
            AdministratorEligibilityProofs.Schema, new AdministratorEligibilityProofs(locator), ct).ConfigureAwait(false);
        return new CompleteAdministratorRestoration(step.CommandId, source, step.Record.Source.Stream,
            root.Operation.RootOperationId, root.Generation, null, step.Record.Intent, intent.MemberId,
            intent.MemberEpisodeId, proofs);
    }

    static NativeFactReference Applied(ProcessStep step, AdministratorTransitionKind kind) =>
        step.Record.Facts.TryGetValue(ApplyReceiver(kind), out var applied)
            ? applied
            : throw new InvalidOperationException($"{ApplyReceiver(kind)} committed no fact to complete.");

    static async Task<ITenantManagerGuardFact> RootAsync(ProcessStep step, ISourceFactResolver resolver,
        CancellationToken ct)
    {
        var resolved = await resolver.ResolveAsync(step.Record.Source.Stream, step.Record.Source.AggregateVersion - 1, ct)
            .ConfigureAwait(false);
        return resolved.Event as ITenantManagerGuardFact
            ?? throw new SourceFactVerificationException("The process root is not a guard fact.");
    }

    static string MutationKind(string commandKind) => commandKind switch
    {
        AdministratorCommandKinds.Suspend => "suspend",
        AdministratorCommandKinds.Deprovision => "deprovision",
        AdministratorCommandKinds.Reinstate => "reinstate",
        _ => throw new SourceFactVerificationException("A neutral member mutation names a member command."),
    };

    /// <summary>Writes the Unit result content a terminal fact references as its operation result.</summary>
    public static async Task<ImmutableContentRef> UnitResultAsync(IImmutableContentStore content, string realm,
        Uuid rootOperationId, CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(rootOperationId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return await content.WriteJsonAsync(realm, Uuid.CreateVersion5(rootOperationId, "mutation-result-content.v1"),
            "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct)
            .ConfigureAwait(false);
    }
}
