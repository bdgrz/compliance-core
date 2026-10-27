using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
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
