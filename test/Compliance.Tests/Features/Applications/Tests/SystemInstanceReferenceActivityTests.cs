using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Applications.History;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class SystemInstanceReferenceActivityTests
{
    [Fact]
    public async Task ShouldReportTransientConflictGivenCommittedButUnprojectedInstanceReference()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        Assert.True(instance.Declare(applicationId, "Production", "production", null, null,
            Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var activity = new EventSourcedApplicationInventoryActivity(new InstanceReader(instance),
            directory, new LegacySystemInstanceSource(directory, new InMemoryEventStore()));
        var boundaries = new GovernedBoundaryReferenceValidator(new NoServices(), activity);
        var controls = new GovernedControlApplicabilityReferenceValidator(activity);

        // Act
        var boundary = await boundaries.ValidateAsync(tenantId, Uuid.CreateVersion4(),
            new BoundaryContent("Scope", "readiness", ["security"],
            [
                new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "system_instance",
                    "Production", instanceId, "Operations", "In scope", false),
            ]));
        var control = await controls.ValidateAsync(tenantId, new ControlDraftContent("Title",
            "Objective", "Description", "Narrative", ["Evidence"], Applicability:
            [
                new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                    "Production", instanceId, "Applies", false),
            ]));
        var absent = await controls.ValidateAsync(tenantId, new ControlDraftContent("Title",
            "Objective", "Description", "Narrative", ["Evidence"], Applicability:
            [
                new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                    "Missing", Uuid.CreateVersion4(), "Applies", false),
            ]));

        // Assert
        foreach (var error in new[] { boundary.Error, control.Error })
        {
            var conflict = Assert.IsType<RequestError>(error);
            Assert.Equal(RequestErrorKind.Conflict, conflict.Kind);
            Assert.True(conflict.IsTransient);
        }
        var missing = Assert.IsType<RequestError>(absent.Error);
        Assert.Equal(RequestErrorKind.Conflict, missing.Kind);
        Assert.False(missing.IsTransient);
    }

    sealed class InstanceReader(DeclaredSystemInstance instance) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredSystemInstance && aggregate.Id == instance.Id
                ? (TAggregate)(Aggregate)instance : aggregate);
    }

    sealed class NoServices : IClientServiceActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid programId, Uuid serviceId,
            CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
