using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests;

/// <summary>Builds well-formed native facts for tests that only need an opened population or a recorded line.</summary>
static class AccessReviewFacts
{
    static readonly DateTimeOffset FactAt = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh in-memory immutable content store.</summary>
    public static IImmutableContentStore Content() =>
        new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));

    /// <summary>The provenance of a root operation on an owner stream under a logical id.</summary>
    public static OperationProvenance Provenance(EventStreamAddress owner, string commandKind,
        Uuid logicalId = default, ActorReference? actor = null, ulong? expectedRevision = null,
        DateTimeOffset? at = null)
    {
        var id = logicalId == Uuid.Empty ? Uuid.CreateVersion4() : logicalId;
        return new OperationProvenance(id, id, null, commandKind,
            actor ?? ActorReference.ForSystemProcess("system", "system"), owner, "initial", expectedRevision,
            new string('a', 64), at ?? FactAt, owner, owner);
    }

    /// <summary>A frozen response reference of the right schema; nothing reads it back.</summary>
    public static ImmutableContentRef Response() => new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", FirstResponses.Schema);

    /// <summary>Opens the population under its own id as the logical operation.</summary>
    public static Result Open(this AccessPopulation population, Uuid applicationId, Uuid systemInstanceId,
        long systemInstanceRevision, DateTimeOffset observedAt, string source, ActorReference actor,
        DateTimeOffset now) => population.Open(Provenance(population.Stream, "OpenAccessPopulation", population.Id),
        applicationId, systemInstanceId, systemInstanceRevision, observedAt, source, actor, now, Response(),
        Response());

    /// <summary>Attaches metadata to a fact the way a committed append would.</summary>
    public static T Attach<T>(T fact, Uuid aggregateId, ulong version, DateTimeOffset occurredOn)
        where T : DomainEvent
    {
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId, version, occurredOn));
        return fact;
    }

    /// <summary>Appends one committed missing-population exception owner stream with its immutable content.</summary>
    public static async Task AppendPopulationExceptionAsync(IEventStore events, IImmutableContentStore content,
        Uuid tenantId, Uuid systemInstanceId, Uuid exceptionId, string reason, DateTimeOffset expiresAt,
        ActorReference approvedBy, DateTimeOffset approvedAt, DateTimeOffset? recordedAt = null)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), AccessPopulationException.Area,
            exceptionId.ToString());
        var view = new AccessPopulationExceptionView(exceptionId, systemInstanceId, reason, expiresAt, approvedBy,
            approvedAt);
        var reference = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
            "AccessPopulationExceptionRecorded.ExceptionContent.v1", view);
        var fact = Attach(new AccessPopulationExceptionRecorded(tenantId, systemInstanceId, 1, reference,
            Provenance(stream, "ExemptMissingAccessPopulation", exceptionId, approvedBy, 0, approvedAt),
            Response(), Response()), exceptionId, 1, recordedAt ?? approvedAt);
        await events.AppendAsync(stream, 0, [fact]);
    }
}
