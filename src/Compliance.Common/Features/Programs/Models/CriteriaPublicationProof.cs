using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>Identifies the committed publication fact for the selected edition.</summary>
public sealed record CriteriaPublicationProof(Uuid EditionId, Uuid EventId, ulong ResourceOffset);
