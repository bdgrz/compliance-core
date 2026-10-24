using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The kind of child a request resolution drains or holds: a response, a comment or a sample selection.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdmittedRequestChildKind>))]
public enum AdmittedRequestChildKind
{
    /// <summary>An auditor response, held by the resolution.</summary>
    [JsonStringEnumMemberName("auditor_response")]
    Response,

    /// <summary>An auditor comment thread, drained by the resolution.</summary>
    [JsonStringEnumMemberName("auditor_comment")]
    Comment,

    /// <summary>A sample selection, drained by the resolution.</summary>
    [JsonStringEnumMemberName("auditor_sample_selection")]
    SampleSelection,
}
