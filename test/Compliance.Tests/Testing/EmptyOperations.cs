using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>An operating reader over an empty immutable content store, for tests that never read operating state.</summary>
static class EmptyOperations
{
    public static ControlOperationsReader Reader(IAggregateReader reader, IDomainEventReader events) => new(reader,
        events, new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));

    /// <summary>An evaluation reader over an empty immutable content store, for tests that never read evaluations.</summary>
    public static ControlEvaluationReader Evaluations(IAggregateReader reader, IDomainEventReader events) => new(
        reader, events, new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
}
