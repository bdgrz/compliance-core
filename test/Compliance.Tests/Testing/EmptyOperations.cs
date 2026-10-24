using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>An operating reader over an empty immutable content store, for tests that never read operating state.</summary>
static class EmptyOperations
{
    public static ControlOperationsReader Reader(IAggregateReader reader, IDomainEventReader events) => new(reader,
        events, new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
}
