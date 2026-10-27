namespace Bdgrz.Compliance.Features.Versioning.Models;

public sealed record VersionConflict(VersionConflictCode Code, string Record,
    long? CurrentRevision = null, Guid? CurrentVersionId = null);
