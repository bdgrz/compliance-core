using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>An exact governed source version and reconciliation state used by a narrative.</summary>
public sealed record SystemDescriptionSourceReference(string Kind, Uuid RecordId, Uuid VersionId,
    long Revision, string ContentSha256, string Status);
