using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record ServiceEngagementBoundaryReference(Uuid BoundaryId, Uuid VersionId, long Revision);
