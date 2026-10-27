using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

public sealed class NativeOwnerHistoryTests
{
    static readonly TrainingRequirementContent Content = new("Security awareness", "Complete original content",
        PolicyAudience.CoreSecurity, null, "manual");

    [Fact]
    public async Task ShouldExcludeAuditSessionFactsGivenNativeHistoryAtCapturedOwnerCut()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, "TR-SEC");
        var auditId = Uuid.CreateVersion4();
        var stream = new EventStreamAddress(host.Tenant.ToString(), "audit-sessions", auditId.ToString());
        DomainEvent audit = new HistoryAudit();
        audit.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), auditId, 0, host.Clock.GetUtcNow(), IsAudit: true));
        await host.Provider.GetRequiredService<IEventStore>().AppendAsync(stream, 0, [audit]);
        var result = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC", Content),
            host.Manager);
        Assert.True(result.IsSuccess, result.Error?.Message);
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));

        // Act
        var facts = await TrainingRequirementHistory.FactsAsync(host.Events, owner);
        var view = await TrainingRequirementRules.ViewAsync(host.Content, host.Events, owner, 1, CancellationToken.None);

        // Assert
        Assert.Equal(1UL, owner.CommittedStreamPosition);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(1, Assert.Single(facts).Version);
        Assert.Equal(Content.CourseName, view!.Content.CourseName);
    }

    [Fact]
    public async Task ShouldKeepOriginalHistoryCutGivenSuccessorCommitsDuringReadComposition()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var captured = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        await TrainingSeed.ReviseAsync(host.Provider, host.Tenant, id, "TR-SEC", 1,
            Content with { CourseName = "Successor" }, host.ManagerActor, host.Clock.GetUtcNow());

        // Act
        var facts = await TrainingRequirementHistory.FactsAsync(host.Events, captured);
        var view = await TrainingRequirementRules.ViewAsync(host.Content, host.Events, captured, null, CancellationToken.None);
        var current = await host.SendAsync(new GetTrainingRequirement(host.Tenant, host.Program, id), host.Manager);

        // Assert
        Assert.Equal(1, Assert.Single(facts).Version);
        Assert.Equal(1, view!.LatestVersion);
        Assert.Equal(Content.CourseName, view.Content.CourseName);
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal(2, current.Value.Version);
        Assert.Equal("Successor", current.Value.Content.CourseName);
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("foreign_stream", false)]
    [InlineData("physical_gap", false)]
    public async Task ShouldClassifyExactSourceFailureGivenMissingOrContradictoryHistory(string fault, bool transient)
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        var reader = new AlteredReader(host.Events, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with
            {
                Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(),
                record.Stream.Area, record.Stream.Resource)
            },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            _ => record,
        });

        // Act
        var error = await Record.ExceptionAsync(async () => await TrainingRequirementHistory.FactsAsync(reader, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(error);
        Assert.Equal(transient, error is ImmutableContentUnavailableException);
    }

    [Fact]
    public async Task ShouldRejectWrongLogicalBodyDigestGivenVerifiedImmutableRequirementBytes()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        var fact = Assert.IsType<TrainingRequirementDefined>(owner.Latest);

        // Act
        var error = await Record.ExceptionAsync(async () => await TrainingRequirementRules.ViewAsync(host.Content,
            fact with { ContentSha256 = new string('f', 64) }, 1, CancellationToken.None));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
    }

    sealed record HistoryAudit : DomainEvent;

    sealed class AlteredReader(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
    }
}
