using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Projections;

public sealed record ArtifactRetentionSourceSnapshot(ArtifactRetentionSource Source, ulong Position,
    DateOnly? PeriodStart = null, DateOnly? PeriodEnd = null);
