using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Programs.Projectors;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramStageProjectionTests
{
    static readonly Uuid Runner = Uuid.CreateVersion4();
    static readonly Uuid Decider = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldEnterTypeIOnlyGivenApprovedEntryDecisionAndNotDeferral()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var approvedProgram = await CreateProgramAsync(fixture, content, tenantId);
        var deferredProgram = await CreateProgramAsync(fixture, content, tenantId);
        await DeferAsync(fixture, content, tenantId, deferredProgram);
        var approvedDecision = await ApproveWithExceptionsAsync(fixture, content, tenantId,
            approvedProgram);
        using var replay = BuildReplayWorker(fixture.Store, content, tenantId);

        // Act
        await replay.StartAsync();
        ProgramView? approved = null;
        ProgramView? deferred = null;
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var scope = replay.Services.CreateScope();
                var directory = scope.ServiceProvider.GetRequiredService<IProgramDirectoryReader>();
                approved = await directory.GetAsync(tenantId, approvedProgram);
                deferred = await directory.GetAsync(tenantId, deferredProgram);
                if (approved?.Stage == "type_i" && deferred is not null)
                    break;
                await Task.Delay(50);
            }
        }
        finally
        {
            await replay.StopAsync();
        }

        // Assert
        Assert.NotNull(approved);
        Assert.Equal("type_i", approved.Stage);
        Assert.Equal("type_ii", approved.NextStage);
        Assert.Equal(approvedDecision, approved.StageDecisionId);
        Assert.Equal(1, approved.Revision);
        Assert.NotNull(approved.StageEnteredBy);
        Assert.NotNull(deferred);
        Assert.Equal("readiness", deferred.Stage);
        Assert.Null(deferred.StageDecisionId);
    }

    static async Task<Uuid> CreateProgramAsync(StoreFixture fixture, IImmutableContentStore content, Uuid tenantId)
    {
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var creation = new RequestContext<CreateProgram>(new CreateProgram(Uuid.CreateVersion4(), tenantId, "SOC 2",
            new ProgramPlan(null, null, null, null, null, null)), actor);
        Assert.True((await new CreateProgramHandler(ProgramSeeds.Operations(fixture.Repository, fixture.Repository, fixture.Store, content))
            .HandleAsync(creation, CancellationToken.None)).IsSuccess);
        return creation.Request.ProgramId;
    }

    static async Task<ReadinessAssessmentView> RecordAssessmentAsync(StoreFixture fixture,
        IImmutableContentStore content, Uuid tenantId, Uuid programId)
    {
        var assessmentId = Uuid.CreateVersion4();
        var asOf = DateTimeOffset.UtcNow.AddMinutes(-1);
        var evaluation = ReadinessRules.Evaluate(programId, asOf, null, [], [],
            new Dictionary<Uuid, ControlVersionView?>());
        await ReadinessSeed.AdoptAsync(fixture.Repository, fixture.Repository, content, tenantId, programId,
            assessmentId, evaluation, asOf, Runner, DateTimeOffset.UtcNow);
        return (await new ReadinessReader(fixture.Repository, content, fixture.Store).ReadViewAsync(tenantId, programId, assessmentId))!;
    }

    static async Task DeferAsync(StoreFixture fixture, IImmutableContentStore content, Uuid tenantId, Uuid programId)
    {
        var assessment = await RecordAssessmentAsync(fixture, content, tenantId, programId);
        await ReadinessSeed.DecideAsync(fixture.Repository, fixture.Repository, content, tenantId, programId,
            assessment.AssessmentId, ReadinessDecisionKind.TypeIEntry, ProgramReadiness.Defer, "Not yet.", Decider,
            DateTimeOffset.UtcNow);
    }

    static async Task<Uuid> ApproveWithExceptionsAsync(StoreFixture fixture, IImmutableContentStore content,
        Uuid tenantId, Uuid programId)
    {
        var assessment = await RecordAssessmentAsync(fixture, content, tenantId, programId);
        foreach (var gap in assessment.Gaps)
            await ReadinessSeed.PlanAsync(fixture.Repository, fixture.Repository, content, tenantId, programId,
                gap.GapId, Runner, new DateOnly(2027, 3, 31), "Close.", Runner, DateTimeOffset.UtcNow);
        await ReadinessSeed.DecideAsync(fixture.Repository, fixture.Repository, content, tenantId, programId,
            assessment.AssessmentId, ReadinessDecisionKind.Readiness, ProgramReadiness.Proceed, "Owned.", Decider,
            DateTimeOffset.UtcNow);
        var unresolved = assessment.Gaps.Select(gap => new TypeIEntryUnresolvedItemView(gap.GapId, gap.Kind,
            gap.Subject, gap.RuleId, gap.Explanation, Runner, new DateOnly(2027, 3, 31), true))
            .ToArray();
        return await ReadinessSeed.DecideAsync(fixture.Repository, fixture.Repository, content, tenantId, programId,
            assessment.AssessmentId, ReadinessDecisionKind.TypeIEntry, ProgramReadiness.ApproveWithExceptions,
            "Acknowledged.", Decider, DateTimeOffset.UtcNow, unresolved);
    }

    static IHost BuildReplayWorker(IDomainEventReader reader, IImmutableContentStore content, Uuid tenant)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Services.AddSingleton(reader);
        builder.Services.AddSingleton(content);
        builder.Services.AddSingleton<IKvClient, InMemoryKvClient>();
        builder.Services.AddSingleton<ITenantDirectory>(new StaticTenantDirectory(
            new TenantId(tenant.ToString())));
        builder.Services.AddScoped<FitzProgramDirectory>();
        builder.Services.AddScoped<IProgramDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzProgramDirectory>());
        builder.Services.AddScoped<IProgramDirectoryReader>(provider =>
            provider.GetRequiredService<FitzProgramDirectory>());
        builder.Services.AddPortiaEvent<ProgramCreated>(2, "bdgrz.program.created");
        builder.Services.AddPortiaEvent<ProgramRevised>(2, "bdgrz.program.revised");
        builder.Services.AddPortiaEvent<ProgramCriteriaEditionSelected>(2,
            "bdgrz.program.criteria.selected");
        builder.Services.AddPortiaEvent<TypeIEntryDecisionRecorded>(1,
            "bdgrz.readiness.type_i_entry.decided");
        builder.Services.AddPortia()
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant,
                options => options.PollInterval = TimeSpan.FromMilliseconds(10))
            .UseSingleProcessWorkloads()
            .AddWorkers();
        return builder.Build();
    }

    sealed class StaticTenantDirectory(TenantId tenant) : ITenantDirectory
    {
        public async IAsyncEnumerable<TenantId> GetActiveTenantsAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            yield return tenant;
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<TenantLifecycleChange> WatchAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            yield break;
        }
    }
}
