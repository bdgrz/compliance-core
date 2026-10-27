using System.Text.Json.Serialization;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>The one typed variant of a participant result that a campaign admits and a participant applies.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CampaignParticipantResultKind>))]
public enum CampaignParticipantResultKind
{
    /// <summary>An acknowledgement of the exact policy version, hash and text.</summary>
    [JsonStringEnumMemberName("policy_acknowledgement")]
    PolicyAcknowledgement,

    /// <summary>A reviewed training completion of the exact requirement version.</summary>
    [JsonStringEnumMemberName("training_completion")]
    TrainingCompletion,

    /// <summary>A bounded exception.</summary>
    [JsonStringEnumMemberName("waiver")]
    Waiver,
}
