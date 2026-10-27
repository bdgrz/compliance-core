using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Projections;

public interface ICriterionApplicabilityWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);

    ValueTask<long> LoadRevisionAsync(Uuid tenantId, CancellationToken ct = default);
}
