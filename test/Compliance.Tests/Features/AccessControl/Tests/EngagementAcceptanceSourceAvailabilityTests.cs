using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Testing.AcceptanceFacts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class EngagementAcceptanceSourceAvailabilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldPreservePendingAuthorizationGivenUnavailablePinnedSourceAndRejectCorruption(bool unavailable)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var request = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(request, partner.Actor)).IsSuccess);
        var record = await StartAsync(host, engagement, request.CommandId);
        var facts = new Dictionary<string, NativeFactReference>();
        foreach (var target in record.Targets)
            foreach (var receiver in EngagementAcceptanceProcess.Definition().Stages[0].Receivers)
            {
                if (await BuildAsync(host, record, receiver, target) is not { } command)
                    continue;
                var held = Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(host, command)).Value);
                facts[ProcessRecord.FactKey(receiver, target)] = held.Fact;
            }
        record = record with { Facts = facts };
        var authorize = Assert.IsType<AuthorizeEngagementPracticeCommitments>(
            await BuildAsync(host, record, nameof(AuthorizeEngagementPracticeCommitments)));
        var services = host.Scope.ServiceProvider;
        var content = new PinnedSourceFault(host.Content, unavailable);
        var handler = new AuthorizeEngagementPracticeCommitmentsHandler(
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateExecutor>(),
            content, services.GetRequiredService<TimeProvider>(), services.GetRequiredService<EngagementAcceptanceVerifier>(),
            services.GetRequiredService<IPlatformUserDirectoryReader>(), services.GetRequiredService<BoundaryReader>());
        var context = new RequestContext<AuthorizeEngagementPracticeCommitments>(authorize, RequestActor.System);
        var owner = new ServiceEngagementIndependence(host.Tenant, engagement);
        var before = (await host.HydrateAsync(owner)).CommittedStreamPosition;

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var after = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));

        // Assert
        Assert.True(content.ReadAttempted);
        Assert.Equal(before, after.CommittedStreamPosition);
        if (unavailable)
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.True(result.Error?.IsTransient);
            content.Enabled = false;
            var retried = await handler.HandleAsync(context, CancellationToken.None);
            Assert.True(retried.IsSuccess, retried.Error?.Message);
            Assert.IsType<ReceiverOutcome.Applied>(retried.Value);
        }
        else
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.IsType<ReceiverOutcome.Rejected>(result.Value);
        }
    }

    sealed class PinnedSourceFault(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public bool Enabled { get; set; } = true;
        public bool ReadAttempted { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (!Enabled || reference.Schema is not (ProfessionalDutyContent.RecordedSchema or FirmStaffContent.StaffSchema))
                return inner.ReadVerifiedAsync(realm, reference, ct);
            ReadAttempted = true;
            return Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The exact pinned source body is not visible yet.")
                : new ImmutableContentIntegrityException("The exact pinned source body is corrupt."));
        }
    }
}
