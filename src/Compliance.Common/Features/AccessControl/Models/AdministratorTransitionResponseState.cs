using System.Text.Json.Serialization;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Whether an administrator-relevant transition finished synchronously or continues asynchronously.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdministratorTransitionResponseState>))]
public enum AdministratorTransitionResponseState
{
    /// <summary>The transition was accepted and continues through guard choreography (HTTP 202).</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>The transition completed synchronously (HTTP 200).</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,
}
