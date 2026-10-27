using Bdgrz.Compliance.Features.Programs.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProductClientServiceHistorySnapshot(ClientService Owner, IReadOnlyList<DomainEvent> Facts);
