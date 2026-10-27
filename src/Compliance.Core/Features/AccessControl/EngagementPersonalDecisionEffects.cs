using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deterministic identities, keys and provenance of the engagement personal decision process.</summary>
static class EngagementPersonalDecisionEffects
{
    public const string ProcessName = "EngagementPersonalDecisionProcess";

    /// <summary>The only generation of a personal decision; a canceled decision is never retried under its identity.</summary>
    public const long Generation = ServiceEngagementIndependence.DecisionGeneration;

    /// <summary>The realm of platform content: staff, duty designation and rule catalog facts.</summary>
    public const string PlatformRealm = "bdgrz";

    /// <summary>The schema of the ratified rule version content a partner evaluation relied on.</summary>
    public const string RatifiedRulesSchema = "PartnerIndependenceEvaluationRecorded.RatifiedRulesContent.v1";

    /// <summary>The schema of the ratification content held in the ratification catalog requirement.</summary>
    public const string RatificationSchema = "independence-rule-ratification-view.v1";

    /// <summary>The schema of the partner evaluation content.</summary>
    public const string EvaluationSchema = "PartnerIndependenceEvaluationRecorded.EvaluationContent.v1";

    /// <summary>The schema of the management acknowledgement content.</summary>
    public const string AcknowledgementSchema = "EngagementManagementAcknowledged.AcknowledgementContent.v1";

    /// <summary>The command id the process driver derives for a receiver step of the root admission.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        EngagementPersonalDecisionIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, intent.Header, owner, acceptedAt);

    /// <summary>The reservation key of the use a personal decision holds on one source owner.</summary>
    public static ReservationKey UseKey(Uuid decisionId, EventStreamAddress engagement, EventStreamAddress target) =>
        new(decisionId, engagement, (ulong)Generation, target,
            SourceUseReservations.PurposeName(SourceUsePurpose.EngagementPersonalDecision));

    /// <summary>The canonical sort key of a source owner stream.</summary>
    public static string StreamKey(EventStreamAddress stream) => $"{stream.Realm}/{stream.Area}/{stream.Resource}";

    /// <summary>The requirement of the intent that names one source owner stream, or null when none is required.</summary>
    public static SourceUseRequirement? RequirementOf(IReadOnlyList<SourceUseRequirement> requirements,
        EventStreamAddress target) => requirements.SingleOrDefault(requirement => requirement.SourceOwner == target);

    /// <summary>The requirement of the intent that names one owner area, or null when the kind does not require it.</summary>
    public static SourceUseRequirement? RequirementOf(IReadOnlyList<SourceUseRequirement> requirements, string area) =>
        requirements.SingleOrDefault(requirement => requirement.SourceOwner.Area == area);

    /// <summary>The acquire receiver that holds the owner of an area, in the order the process lists them.</summary>
    public static string? AcquireReceiver(string area) => area switch
    {
        ClientServiceIndependence.Area => nameof(AuthorizeClientServicePersonalDecisionUse),
        FirmStaffRecord.Area => nameof(AuthorizeFirmStaffRecordEngagementUse),
        FirmProfessionalDutyDesignation.Area => nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse),
        IndependenceRuleCatalog.Area => nameof(AuthorizeIndependenceRuleCatalogEngagementUse),
        IndependenceRuleRatificationCatalog.Area => nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
        _ => null,
    };

    /// <summary>The areas of the acquire receivers in process order.</summary>
    public static IReadOnlyList<string> AcquireAreas { get; } =
    [
        ClientServiceIndependence.Area,
        FirmStaffRecord.Area,
        FirmProfessionalDutyDesignation.Area,
        IndependenceRuleCatalog.Area,
        IndependenceRuleRatificationCatalog.Area,
    ];
}
