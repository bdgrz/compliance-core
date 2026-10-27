using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationImportVisibilityReadConsistencyTests
{
    [Fact]
    public async Task ShouldReturnRetryableConflictGivenChangedVisibilityCursor()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var consistency = new ApplicationImportVisibilityReadConsistency(
            new FitzApplicationDirectory(new InMemoryKvClient()), new PendingEvents(tenant, []));
        var behavior = new ApplicationImportVisibilityBehavior(consistency);

        // Act
        var result = await behavior.HandleAsync(new RequestContext<ListApplications>(new ListApplications(tenant), RequestActor.System),
            _ => ValueTask.FromException<Result<Page<ApplicationView>>>(new ApplicationImportVisibilityChangedException()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    [Fact]
    public async Task ShouldBlockHandlerGivenRegisteredReadBehaviorAndPendingCommit()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDirectoryReader>(new FitzApplicationDirectory(new InMemoryKvClient()));
        services.AddSingleton<IDomainEventReader>(new PendingEvents(tenant, [Marker(tenant)]));
        services.AddScoped<ApplicationImportVisibilityReadConsistency>();
        services.AddPortia().AddRequestHandler<EmptyListHandler>()
            .AddRequestPipelineBehavior<ApplicationImportVisibilityBehavior>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new ListApplications(tenant), RequestActor.System);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.False(scope.ServiceProvider.GetRequiredService<EmptyListHandler>().WasCalled);
    }

    [Fact]
    public async Task ShouldRejectProjectionReadGivenPendingCommitMarker()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var events = new PendingEvents(tenant, [Marker(tenant)]);
        var consistency = new ApplicationImportVisibilityReadConsistency(directory, events);

        // Act
        var result = await consistency.CaptureAsync(tenant, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    [Fact]
    public async Task ShouldRejectReadResultGivenCommitArrivesAfterCapture()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var events = new PendingEvents(tenant, []);
        var consistency = new ApplicationImportVisibilityReadConsistency(directory, events);
        var captured = await consistency.CaptureAsync(tenant, CancellationToken.None);
        Assert.True(captured.IsSuccess);
        events.Items = [Marker(tenant)];

        // Act
        var result = await consistency.ConfirmAsync(tenant, captured.Value, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    static ApplicationImportCommitted Marker(Uuid tenant)
    {
        var stream = new EventStreamAddress(tenant.ToString(), ApplicationImportBatch.Area,
            Uuid.CreateVersion4().ToString());
        var content = new ImmutableContentRef(Uuid.CreateVersion4(), new string('0', 64), 2, "application/json",
            "import-test.v1");
        return new(tenant, "manual", "applications", Uuid.CreateVersion4(), 6, new string('0', 64), content,
            DateTimeOffset.UtcNow, new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null,
                "CommitApplicationImportBatch", ActorReference.ForSystemProcess("test", "test"), stream, "initial",
                null, new string('0', 64), DateTimeOffset.UtcNow, stream, stream), content, 1, content, null, null);
    }

    internal sealed class EmptyListHandler : IRequestHandler<ListApplications, Page<ApplicationView>>
    {
        public bool WasCalled { get; private set; }

        public ValueTask<Result<Page<ApplicationView>>> HandleAsync(IRequestContext<ListApplications> context,
            CancellationToken ct)
        {
            WasCalled = true;
            return ValueTask.FromResult(Result<Page<ApplicationView>>.Success(new Page<ApplicationView>([], null)));
        }
    }

    sealed class PendingEvents(Uuid tenant, DomainEvent[] items) : IDomainEventReader
    {
        public DomainEvent[] Items { get; set; } = items;

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong offset = 0, CancellationToken ct = default) =>
            ReadAsync(EventStreamPattern.ForPattern(tenant.ToString()), default, ct);

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor cursor = default, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            foreach (var ev in Items)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(new EventStreamAddress(tenant.ToString(), "application_imports",
                    "source"), ev, 0, default);
            }
        }
    }
}
