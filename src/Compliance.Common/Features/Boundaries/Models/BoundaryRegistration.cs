using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryRegistration(Uuid BoundaryId, Uuid DraftVersionId);
