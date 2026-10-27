using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Contracts;

/// <summary>A disclosure mutation bound to one exact evidence artifact.</summary>
public interface IArtifactDisclosureRequest : IClientManagementMutationRequest
{
    Uuid ArtifactId { get; }
}
