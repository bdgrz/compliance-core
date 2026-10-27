using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Authorizes and records an attributable download of one available artifact.</summary>
[Discriminator("bdgrz.evidence.artifact.download.issue", 1)]
public sealed record IssueArtifactDownload(Uuid TenantId, Uuid ArtifactId, string Purpose)
    : IRequest<EvidenceArtifactDownloadLink>, IClientManagementMutationRequest, ICallable
{
    /// <summary>The active handoff scope when a consultant downloads selected evidence.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? ProgramId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? AdvisorHandoffId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdvisorRecordVersion? SelectedSource { get; init; }
}
