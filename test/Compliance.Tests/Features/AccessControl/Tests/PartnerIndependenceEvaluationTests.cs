using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PartnerIndependenceEvaluationTests
{
    [Fact]
    public async Task ShouldRetainConditionalPartnerEvaluationBoundToExactDraftAndCompleteClientHistoryGivenCurrentDuty()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await EvaluationAsync(host, engagement, partner);
        var stale = request with { ExpectedServiceHistoryDigest = new string('0', 64) };

        // Act
        var staleResult = await host.HttpAsync(stale, partner.Actor);
        var admitted = await host.HttpAsync(request, partner.Actor);
        var process = await RunAsync(host.Provider, host.Tenant, engagement, request.EvaluationId);
        var events = await host.EventsAsync<DomainEvent>(
            new PartnerIndependenceDecision(host.Tenant, request.EvaluationId).Stream);
        var replay = new AggregateScenario<PartnerIndependenceDecision>(
            new PartnerIndependenceDecision(host.Tenant, request.EvaluationId)).Given(events.ToArray()).Aggregate;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, staleResult.Error?.Kind);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, process.State);
        var recorded = Assert.IsType<PartnerIndependenceEvaluationRecorded>(Assert.Single(events));
        Assert.Equal(recorded.Metadata.EventId, replay.Recorded!.Metadata.EventId);
        var evaluation = await host.Content.ReadJsonAsync<PartnerIndependenceEvaluationView>(host.Tenant.ToString(),
            replay.Recorded.EvaluationContent);
        Assert.Equal(host.Tenant, evaluation.TenantId);
        Assert.Equal(request.EngagementId, evaluation.EngagementId);
        Assert.Equal(request.ExpectedEngagementRevision, evaluation.DraftRevision);
        Assert.Equal(request.RuleVersion, evaluation.RuleVersion);
        Assert.Equal(IndependenceSourceDigest.RuleContent(partner.Rules), evaluation.RuleContentDigest);
        Assert.Equal(request.ExpectedServiceHistoryDigest, evaluation.ServiceHistoryDigest);
        Assert.Single(evaluation.CompleteServiceHistory);
        Assert.Equal("partner_evaluation_required", evaluation.DecisionCode);
        Assert.Equal("conditionally_compatible", evaluation.Outcome);
        Assert.Equal(partner.Staff.StaffMemberId, evaluation.PartnerStaffMemberId);
        Assert.Equal(partner.Staff.Revision, evaluation.PartnerDirectoryStaffRevision);
        Assert.Equal(partner.Duty.DesignationId, evaluation.DutyDesignationId);
        Assert.Equal("firm_staff", evaluation.Actor.Kind);
        Assert.Equal(partner.Staff.UserId.ToString(), evaluation.Actor.Id);
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenImpairingServiceHistoryThatNoPartnerEvaluationOverrides()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host, IndependenceHost.DefaultRules);
        var lead = await host.RegisterStaffAsync("attest");
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service("design", new DateOnly(2025, 12, 1))))).IsSuccess);
        var request = await EvaluationAsync(host, engagement, partner);

        // Act
        var result = await host.HttpAsync(request, partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAdmitted>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("operation")]
    [InlineData("revision")]
    public async Task ShouldRejectMisboundEvaluationGivenCorruptedReplay(string corruption)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(request, partner.Actor)).IsSuccess);
        await RunAsync(host.Provider, host.Tenant, engagement, request.EvaluationId);
        var recorded = Assert.Single(await host.EventsAsync<PartnerIndependenceEvaluationRecorded>(
            new PartnerIndependenceDecision(host.Tenant, request.EvaluationId).Stream));
        var corrupted = corruption switch
        {
            "operation" => recorded with { OperationId = Uuid.CreateVersion4() },
            "revision" => recorded with { Revision = 2 },
            _ => recorded with { Operation = recorded.Operation with { Owner = new EventStreamAddress("another", "streams", "other") } },
        };

        // Act
        var replay = () => new AggregateScenario<PartnerIndependenceDecision>(
            new PartnerIndependenceDecision(host.Tenant, request.EvaluationId)).Given(corrupted);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }
}
