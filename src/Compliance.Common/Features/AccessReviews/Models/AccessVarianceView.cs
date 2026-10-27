using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>An accepted population explained by the expectations approved and effective at its observation.</summary>
public sealed record AccessVarianceView(Uuid TenantId, Uuid PopulationId, Uuid SnapshotId,
    Uuid CalculationId, DateTimeOffset ObservedAt, IReadOnlyList<AccessVarianceItemView> Items);
