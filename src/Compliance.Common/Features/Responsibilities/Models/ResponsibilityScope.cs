using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities.Models;

public sealed record ResponsibilityScope(string RecordType, Uuid RecordId,
    Uuid VersionId, long Revision);
