using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deterministic identities, keys and provenance of the engagement acceptance process.</summary>
static class EngagementAcceptanceEffects
{
    public const string ProcessName = "EngagementAcceptanceProcess";

    /// <summary>The only generation of an acceptance or staff assignment; a rejected operation is never retried.</summary>
    public const long Generation = ServiceEngagementIndependence.AcceptanceGeneration;

    /// <summary>The schema of the acceptance view content a recorded acceptance or assignment carries.</summary>
    public const string AcceptanceSchema = "ServiceEngagementAcceptanceRecorded.AcceptanceContent.v1";

    /// <summary>The schema of the terminal result of an acceptance or staff assignment.</summary>
    public const string ResultSchema = "ServiceEngagementAcceptanceRecorded.OperationResult.v1";

    /// <summary>The schema of the directory staff snapshot a staff assignment relied on.</summary>
    public const string AssignedStaffSchema = "ServiceEngagementStaffAssigned.AssignedStaffContent.v1";

    /// <summary>The schema of the partner staff snapshot a staff assignment relied on.</summary>
    public const string PartnerStaffSchema = "ServiceEngagementStaffAssigned.PartnerStaffContent.v1";

    /// <summary>The schema of the partner designation snapshot a staff assignment relied on.</summary>
    public const string PartnerDesignationSchema = "ServiceEngagementStaffAssigned.PartnerDesignationContent.v1";

    /// <summary>The command id the process driver derives for a receiver step of the root admission.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        EngagementIndependenceIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, intent.Header, owner, acceptedAt);

    /// <summary>The reservation key of the use an acceptance or assignment holds on one owner.</summary>
    public static ReservationKey UseKey(Uuid operationId, EventStreamAddress engagement, EventStreamAddress target) =>
        new(operationId, engagement, (ulong)Generation, target,
            SourceUseReservations.PurposeName(PurposeOf(target.Area)));

    /// <summary>
    ///     The purpose an owner records for a use: the service owner and the practice owner have their own, the
    ///     shared source owners record every engagement use under the one purpose their reducers declare.
    /// </summary>
    public static SourceUsePurpose PurposeOf(string area) => area switch
    {
        ClientServiceIndependence.Area => SourceUsePurpose.EngagementAcceptance,
        PracticeIndependenceCommitment.Area => SourceUsePurpose.ActualStaffAssignment,
        _ => SourceUsePurpose.EngagementPersonalDecision,
    };

    /// <summary>The canonical sort key of one owner stream.</summary>
    public static string StreamKey(EventStreamAddress stream) => PracticeSubjects.Key(stream);

    /// <summary>The acquire receiver that holds the owner of an area; practice subjects are reserved instead.</summary>
    public static string? AcquireReceiver(string area) => area switch
    {
        ClientServiceIndependence.Area => nameof(AcquireEngagementServiceEpoch),
        FirmStaffRecord.Area => nameof(AuthorizeFirmStaffRecordEngagementUse),
        FirmProfessionalDutyDesignation.Area => nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse),
        IndependenceRuleCatalog.Area => nameof(AuthorizeIndependenceRuleCatalogEngagementUse),
        IndependenceRuleRatificationCatalog.Area => nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
        PracticeIndependenceCommitment.Area => nameof(ReservePracticeIndependence),
        _ => null,
    };

    /// <summary>The settle receiver that ends the use of an area.</summary>
    public static string? SettleReceiver(string area) => area switch
    {
        ClientServiceIndependence.Area => nameof(ReleaseEngagementServiceEpoch),
        FirmStaffRecord.Area => nameof(SettleFirmStaffRecordEngagementUse),
        FirmProfessionalDutyDesignation.Area => nameof(SettleFirmProfessionalDutyDesignationEngagementUse),
        IndependenceRuleCatalog.Area => nameof(SettleIndependenceRuleCatalogEngagementUse),
        IndependenceRuleRatificationCatalog.Area => nameof(SettleIndependenceRuleRatificationCatalogEngagementUse),
        PracticeIndependenceCommitment.Area => nameof(SettlePracticeIndependenceReservation),
        _ => null,
    };

    /// <summary>The wire name of a practice for an engagement practice string.</summary>
    public static string PracticeOf(EngagementPractice practice) => practice == EngagementPractice.Attest
        ? PracticeIndependenceCommitment.Attest
        : PracticeIndependenceCommitment.Advisory;
}
