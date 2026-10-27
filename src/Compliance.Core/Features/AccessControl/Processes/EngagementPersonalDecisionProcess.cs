using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The ordered engagement personal decision process. Acquire holds every source use the kind requires (a
///     management acknowledgement only the service history; a partner evaluation also the partner's staff record and
///     duty designation and both rule catalogs), authorize is the irreversible fact that fixes the decision instant,
///     materialize records the independent decision, and settle releases the uses and the engagement content pin. A
///     receiver that proves the decision can never proceed rejects it: the abort receiver then cancels it before
///     authorization and settle fences every use, held or not. Receivers a kind did not select are skipped.
/// </summary>
static class EngagementPersonalDecisionProcess
{
    public const string Acquire = "acquire";
    public const string Authorize = "authorize";
    public const string Materialize = "materialize";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(EngagementPersonalDecisionEffects.ProcessName,
        [nameof(EngagementPersonalDecisionAdmitted)], true,
        [
            new ProcessStageDefinition(Acquire,
            [
                nameof(AuthorizeClientServicePersonalDecisionUse),
                nameof(AuthorizeFirmStaffRecordEngagementUse),
                nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse),
                nameof(AuthorizeIndependenceRuleCatalogEngagementUse),
                nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
            ], "Canonical key order from the frozen RequiredSourceUses manifest; management acknowledgement acquires only the uses its kind requires."),
            new ProcessStageDefinition(Authorize, [nameof(AuthorizeEngagementPersonalDecision)],
                "Irreversible; cancellation is only possible before this fact."),
            new ProcessStageDefinition(Materialize,
            [
                nameof(MaterializeEngagementManagementAcknowledgement),
                nameof(MaterializePartnerIndependenceDecision),
            ]),
            new ProcessStageDefinition(Settle,
            [
                nameof(SettleClientServicePersonalDecisionUse),
                nameof(SettleFirmStaffRecordEngagementUse),
                nameof(SettleFirmProfessionalDutyDesignationEngagementUse),
                nameof(SettleIndependenceRuleCatalogEngagementUse),
                nameof(SettleIndependenceRuleRatificationCatalogEngagementUse),
                nameof(SettleEngagementPersonalDecision),
            ]),
        ], [nameof(CancelEngagementPersonalDecision)], CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenantId = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var decisionId = record.Key.RootOperationId;
        var engagement = record.Source.Stream;
        var engagementId = Uuid.Parse(engagement.Resource, CultureInfo.InvariantCulture);

        EngagementPersonalDecisionIntent intent;
        IReadOnlyList<SourceUseRequirement> requirements;
        try
        {
            intent = await content.ReadJsonAsync<EngagementPersonalDecisionIntent>(record.Key.Realm, record.Intent, ct)
                .ConfigureAwait(false);
            requirements = await content.ReadSelectionAsync<SourceUseRequirement>(record.Key.Realm,
                intent.RequiredSourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen personal decision is not readable yet: {exception.Message}");
        }

        var aborting = record.State == ProcessState.Aborting;
        var disposition = aborting
            ? SourceUseSettlementDisposition.Canceled
            : SourceUseSettlementDisposition.IrreversibleDecision;
        var generation = EngagementPersonalDecisionEffects.Generation;
        SourceUseRequirement? Required(string receiver) => requirements.SingleOrDefault(item =>
            EngagementPersonalDecisionEffects.AcquireReceiver(item.SourceOwner.Area) == receiver);
        SourceUseRequirement? RequiredForSettle(string receiver) => Required(receiver.Replace("Settle", "Authorize",
            StringComparison.Ordinal));

        switch (step.Receiver)
        {
            case nameof(AuthorizeClientServicePersonalDecisionUse) when Required(step.Receiver) is { } use:
                return new AuthorizeClientServicePersonalDecisionUse(step.CommandId, record.Source, use.SourceOwner,
                    decisionId, generation, null, record.Intent, tenantId, use.ContentEvent.EventId,
                    checked((long)use.ResourceRevision), engagementId);
            case nameof(AuthorizeFirmStaffRecordEngagementUse) when Required(step.Receiver) is { } use:
                return new AuthorizeFirmStaffRecordEngagementUse(step.CommandId, record.Source, use.SourceOwner,
                    decisionId, generation, null, record.Intent,
                    Uuid.Parse(use.SourceOwner.Resource, CultureInfo.InvariantCulture), use.ContentEvent.EventId,
                    use.ResourceRevision, engagementId);
            case nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse) when Required(step.Receiver) is { } use:
                return new AuthorizeFirmProfessionalDutyDesignationEngagementUse(step.CommandId, record.Source,
                    use.SourceOwner, decisionId, generation, null, record.Intent,
                    Uuid.Parse(intent.Header.OriginalActor.Id, CultureInfo.InvariantCulture),
                    use.ContentEvent.EventId, use.ResourceRevision, engagementId);
            case nameof(AuthorizeIndependenceRuleCatalogEngagementUse) when Required(step.Receiver) is { } use:
                return new AuthorizeIndependenceRuleCatalogEngagementUse(step.CommandId, record.Source,
                    use.SourceOwner, decisionId, generation, null, record.Intent, IndependenceRuleCatalog.Identity,
                    use.ContentEvent.EventId, use.ResourceRevision, engagementId);
            case nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse) when Required(step.Receiver) is
            { } use:
                return new AuthorizeIndependenceRuleRatificationCatalogEngagementUse(step.CommandId, record.Source,
                    use.SourceOwner, decisionId, generation, null, record.Intent,
                    IndependenceRuleRatificationCatalog.Identity, use.ContentEvent.EventId, use.ResourceRevision,
                    engagementId);
            case nameof(AuthorizeEngagementPersonalDecision):
                return new AuthorizeEngagementPersonalDecision(step.CommandId, record.Source, engagement, decisionId,
                    generation, null, record.Intent, engagementId,
                    await ProofsAsync(record, requirements, tenantId, content, ct).ConfigureAwait(false));
            case nameof(MaterializeEngagementManagementAcknowledgement)
                when intent.Kind == EngagementPersonalDecisionKind.ManagementAcknowledgement:
                return new MaterializeEngagementManagementAcknowledgement(step.CommandId, AuthorizedFact(record),
                    new EngagementManagementAcknowledgement(tenantId, decisionId).Stream, decisionId, generation, null,
                    record.Intent, decisionId,
                    await ProofsAsync(record, requirements, tenantId, content, ct).ConfigureAwait(false));
            case nameof(MaterializePartnerIndependenceDecision)
                when intent.Kind == EngagementPersonalDecisionKind.PartnerEvaluation:
                return new MaterializePartnerIndependenceDecision(step.CommandId, AuthorizedFact(record),
                    new PartnerIndependenceDecision(tenantId, decisionId).Stream, decisionId, generation, null,
                    record.Intent, decisionId,
                    await ProofsAsync(record, requirements, tenantId, content, ct).ConfigureAwait(false));
            case nameof(SettleClientServicePersonalDecisionUse) when RequiredForSettle(step.Receiver) is { } use:
                return new SettleClientServicePersonalDecisionUse(step.CommandId, record.Source, use.SourceOwner,
                    decisionId, generation, null, record.Intent, tenantId, engagementId, disposition);
            case nameof(SettleFirmStaffRecordEngagementUse) when RequiredForSettle(step.Receiver) is { } use:
                return new SettleFirmStaffRecordEngagementUse(step.CommandId, record.Source, use.SourceOwner,
                    decisionId, generation, null, record.Intent,
                    Uuid.Parse(use.SourceOwner.Resource, CultureInfo.InvariantCulture), engagementId, disposition);
            case nameof(SettleFirmProfessionalDutyDesignationEngagementUse)
                when RequiredForSettle(step.Receiver) is { } use:
                return new SettleFirmProfessionalDutyDesignationEngagementUse(step.CommandId, record.Source,
                    use.SourceOwner, decisionId, generation, null, record.Intent,
                    Uuid.Parse(intent.Header.OriginalActor.Id, CultureInfo.InvariantCulture), engagementId,
                    disposition);
            case nameof(SettleIndependenceRuleCatalogEngagementUse) when RequiredForSettle(step.Receiver) is { } use:
                return new SettleIndependenceRuleCatalogEngagementUse(step.CommandId, record.Source, use.SourceOwner,
                    decisionId, generation, null, record.Intent, IndependenceRuleCatalog.Identity, engagementId,
                    disposition);
            case nameof(SettleIndependenceRuleRatificationCatalogEngagementUse)
                when RequiredForSettle(step.Receiver) is { } use:
                return new SettleIndependenceRuleRatificationCatalogEngagementUse(step.CommandId, record.Source,
                    use.SourceOwner, decisionId, generation, null, record.Intent,
                    IndependenceRuleRatificationCatalog.Identity, engagementId, disposition);
            case nameof(SettleEngagementPersonalDecision) when !aborting:
                return new SettleEngagementPersonalDecision(step.CommandId, MaterializedFact(record, intent), engagement,
                    decisionId, generation, null, record.Intent, engagementId);
            case nameof(CancelEngagementPersonalDecision):
                return new CancelEngagementPersonalDecision(step.CommandId, record.Source, engagement, decisionId,
                    generation, null, record.Intent, engagementId);
            default:
                return null;
        }
    }

    static NativeFactReference AuthorizedFact(ProcessRecord record) =>
        record.Facts.TryGetValue(nameof(AuthorizeEngagementPersonalDecision), out var fact)
            ? fact
            : throw new SourceFactVerificationException("The authorization is not recorded in the process yet.");

    static NativeFactReference MaterializedFact(ProcessRecord record, EngagementPersonalDecisionIntent intent) =>
        record.Facts.TryGetValue(intent.Kind == EngagementPersonalDecisionKind.ManagementAcknowledgement
            ? nameof(MaterializeEngagementManagementAcknowledgement)
            : nameof(MaterializePartnerIndependenceDecision), out var fact)
            ? fact
            : throw new SourceFactVerificationException("The materialization is not recorded in the process yet.");

    /// <summary>
    ///     The grant facts the acquire stage produced, in the order of the frozen requirement manifest. The content is
    ///     keyed by the root admission, so every step that needs it writes the identical bytes to the identical id.
    /// </summary>
    static async Task<ImmutableContentRef> ProofsAsync(ProcessRecord record,
        IReadOnlyList<SourceUseRequirement> requirements, Uuid tenantId, IImmutableContentStore content,
        CancellationToken ct)
    {
        var proofs = new List<NativeFactReference>(requirements.Count);
        foreach (var requirement in requirements)
        {
            var receiver = EngagementPersonalDecisionEffects.AcquireReceiver(requirement.SourceOwner.Area)
                ?? throw new SourceFactVerificationException("A required source use names an unknown owner.");
            proofs.Add(record.Facts.TryGetValue(receiver, out var fact)
                ? fact
                : throw new SourceFactVerificationException($"{receiver} has not produced its grant yet."));
        }

        return await content.WriteJsonAsync(IndependenceContent.Realm(tenantId),
            Uuid.CreateVersion5(record.Source.EventId, "engagement-personal-decision-source-use-proofs"),
            EngagementPersonalDecisionIntent.ProofSchema, proofs.ToArray(), ct).ConfigureAwait(false);
    }
}
