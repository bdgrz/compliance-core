using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Authorizes an admitted personal decision irreversibly. Every required source use is re-proven from its
///     authoritative owner (the exact grant is active and is the one the acquire stage produced), every temporal
///     validity interval is judged at the server-frozen decision instant, and only then is the fact appended. A
///     decision that was canceled stays fenced; an interval that does not hold rejects the whole process so the
///     abort path cancels it and settles every use.
/// </summary>
sealed class AuthorizeEngagementPersonalDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementPersonalDecisionVerifier verifier)
    : IRequestHandler<AuthorizeEngagementPersonalDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<AuthorizeEngagementPersonalDecision> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != EngagementPersonalDecisionEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The authorization names another operation or generation.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId,
            command.EngagementId, command.CommandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, decision, intent) = (loaded.Owner!, loaded.Decision!, loaded.Intent!);
        if (command.Target != owner.Stream)
            return SourceUseReceiverSupport.Permanent("The authorization names another engagement stream.");

        var now = clock.GetUtcNow();
        var provenance = EngagementPersonalDecisionEffects.Child(command.CommandId,
            nameof(AuthorizeEngagementPersonalDecision), command.Source.EventId, intent, owner.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        if (decision.Canceled is not null)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(
                "The personal decision was canceled; authorization is fenced.",
                NativeFactReference.Of(owner.Stream, decision.Canceled)));
        if (decision.Authorized is not null)
            return SourceUseReceiverSupport.Superseded("The personal decision is already authorized.",
                NativeFactReference.Of(owner.Stream, decision.Authorized));

        var held = await VerifyHoldsAsync(loaded, command.SourceUseProofs, ct).ConfigureAwait(false);
        if (held is not null)
            return Result<ReceiverOutcome>.Failure(held);
        if (await ValidAtAsync(intent, loaded.Requirements!, now, ct).ConfigureAwait(false) is { } invalid)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(invalid, null));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ServiceEngagementIndependence(owner.TenantId,
                    command.EngagementId, command.CommandId, command.OperationId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false,
                                        null)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.ReplayCut.Digest != owner.ReplayCut.Digest)
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The engagement native prefix changed during receiver preparation; retry from its exact current cut.", true)));

                        if (fresh.DecisionOf(command.OperationId) is { Canceled: { } canceled })
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, canceled), false,
                                "The personal decision was canceled; authorization is fenced.")));
                        var authorized = fresh.AuthorizePersonalDecision(provenance, now, command.SourceUseProofs);
                        return authorized.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true, null)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(authorized.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Fenced is { } fenced)
                    return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(fenced, step.Value.Fact));
                return step.Value.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery or cancellation won the append; rehydrate and decide again.
            }
        }
    }

    /// <summary>Every required use is held by its exact grant, resolved from the owner and not from the proof alone.</summary>
    async ValueTask<RequestError?> VerifyHoldsAsync(LoadedDecision loaded, ImmutableContentRef proofsReference,
        CancellationToken ct)
    {
        var (engagement, intent, requirements) = (loaded.Owner!.Stream, loaded.Intent!, loaded.Requirements!);
        var tenantId = EngagementPersonalDecisionIntent.TenantOf(intent.Header);
        NativeFactReference[] proofs;
        try
        {
            proofs = await content.ReadJsonAsync<NativeFactReference[]>(IndependenceContent.Realm(tenantId),
                proofsReference, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return EngagementPersonalDecisionVerifier.Permanent(
                $"The source use proofs cannot be verified: {exception.Message}");
        }

        if (proofs.Length != requirements.Count)
            return EngagementPersonalDecisionVerifier.Permanent(
                "The proofs do not cover exactly the frozen required source uses.");
        foreach (var requirement in requirements)
        {
            var key = EngagementPersonalDecisionEffects.UseKey(intent.DecisionId, engagement, requirement.SourceOwner);
            var source = await HydrateAsync(requirement.SourceOwner, intent, tenantId, key, ct).ConfigureAwait(false);
            if (source is null || source.Stream != requirement.SourceOwner)
                return EngagementPersonalDecisionVerifier.Permanent(
                    "A required source use names an owner that cannot be resolved.");
            if (source.Uses.Find(key) is not { IsActive: true } entry)
                return EngagementPersonalDecisionVerifier.Transient(
                    "A required source use is not held yet; authorization waits for the complete set.");
            if (!proofs.Contains(NativeFactReference.Of(source.Stream, entry.GrantFact!)))
                return EngagementPersonalDecisionVerifier.Permanent(
                    "The proofs do not name the exact grant that holds a required source use.");
        }

        return null;
    }

    async ValueTask<ISourceUseOwner?> HydrateAsync(EventStreamAddress stream, EngagementPersonalDecisionIntent intent,
        Uuid tenantId, ReservationKey key, CancellationToken ct)
    {
        switch (stream.Area)
        {
            case ClientServiceIndependence.Area:
                return await reader.HydrateAsync(new ClientServiceIndependence(tenantId).ForSourceUses(key), ct).ConfigureAwait(false);
            case FirmStaffRecord.Area when Uuid.TryParse(stream.Resource, out var staffId):
                return await reader.HydrateAsync(new FirmStaffRecord(staffId).ForSourceUses(key), ct).ConfigureAwait(false);
            case FirmProfessionalDutyDesignation.Area when Uuid.TryParse(intent.Header.OriginalActor.Id, out var userId):
                return await reader.HydrateAsync(new FirmProfessionalDutyDesignation(userId,
                    FirmProfessionalDuty.EngagementPartner, tenantId).ForSourceUses(key), ct).ConfigureAwait(false);
            case IndependenceRuleCatalog.Area:
                return await reader.HydrateAsync(new IndependenceRuleCatalog().ForSourceUses(key), ct).ConfigureAwait(false);
            case IndependenceRuleRatificationCatalog.Area:
                return await reader.HydrateAsync(new IndependenceRuleRatificationCatalog().ForSourceUses(key), ct).ConfigureAwait(false);
            default:
                return null;
        }
    }

    /// <summary>Judges every temporal validity interval of the partner's sources at the frozen decision instant.</summary>
    async ValueTask<string?> ValidAtAsync(EngagementPersonalDecisionIntent intent,
        IReadOnlyList<SourceUseRequirement> requirements, DateTimeOffset decisionAt, CancellationToken ct)
    {
        if (intent.Kind != EngagementPersonalDecisionKind.PartnerEvaluation)
            return null;
        try
        {
            if (EngagementPersonalDecisionEffects.RequirementOf(requirements, FirmProfessionalDutyDesignation.Area) is
                { } dutyRequirement)
            {
                var duty = await ProfessionalDutyContent.ReadDesignationAsync(content, dutyRequirement.RequiredFacts, ct)
                    .ConfigureAwait(false);
                if (duty.DesignatedAt > decisionAt || duty.NotBefore > decisionAt || duty.ExpiresAt <= decisionAt)
                    return "The partner designation is not valid at the decision instant.";
            }

            if (EngagementPersonalDecisionEffects.RequirementOf(requirements, FirmStaffRecord.Area) is
                { } staffRequirement)
            {
                var staff = await FirmStaffContent.ReadStaffAsync(content, staffRequirement.RequiredFacts, ct)
                    .ConfigureAwait(false);
                if (staff.RecordedAt > decisionAt)
                    return "The partner staff record is dated after the decision instant.";
            }

            if (EngagementPersonalDecisionEffects.RequirementOf(requirements, IndependenceRuleCatalog.Area) is
                { } rulesRequirement)
            {
                var rules = await content.ReadJsonAsync<IndependenceRuleVersionView>(
                    EngagementPersonalDecisionEffects.PlatformRealm, rulesRequirement.RequiredFacts, ct)
                    .ConfigureAwait(false);
                if (rules.RecordedAt > decisionAt || rules.Ratification?.RecordedAt > decisionAt)
                    return "The ratified rules are dated after the decision instant.";
            }
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return $"The pinned source content cannot be verified: {exception.Message}";
        }

        return null;
    }

    sealed record Step(NativeFactReference Fact, bool Applied, string? Fenced);
}
