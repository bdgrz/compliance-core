using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Aggregates;
namespace Bdgrz.Compliance.Features.Programs.Projections;

public sealed record ProductClientServiceHistorySnapshot(ClientService Owner, IReadOnlyList<DomainEvent> Facts);
