using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>A reaction context for an event read back from a store, as the system principal.</summary>
class RecordReactorContext(DomainEventRecord source) : IReactorContext
{
    public DomainEventRecord Source => source;

    public ClaimsPrincipal Actor => RequestActor.System;

    public Uuid ExecutionId { get; } = Uuid.CreateVersion4();

    public Uuid CorrelationId { get; } = Uuid.CreateVersion4();

    public Uuid CauseId => source.Event.Metadata.EventId;

    public DateTimeOffset StartedAt => new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
}
