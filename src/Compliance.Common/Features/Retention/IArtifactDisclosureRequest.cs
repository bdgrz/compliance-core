using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>A disclosure mutation bound to one exact evidence artifact.</summary>
public interface IArtifactDisclosureRequest : IClientManagementMutationRequest
{
    Uuid ArtifactId { get; }
}
