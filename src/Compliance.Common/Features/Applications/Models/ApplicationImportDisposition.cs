using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>How an import batch ended for a source or target that was held for it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApplicationImportDisposition>))]
public enum ApplicationImportDisposition
{
    /// <summary>The batch published every target together.</summary>
    [JsonStringEnumMemberName("published")]
    Published,

    /// <summary>The batch was canceled after every target was fenced.</summary>
    [JsonStringEnumMemberName("canceled")]
    Canceled,

    /// <summary>The batch failed; no target was published.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
