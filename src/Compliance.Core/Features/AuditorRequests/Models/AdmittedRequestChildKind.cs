using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

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
