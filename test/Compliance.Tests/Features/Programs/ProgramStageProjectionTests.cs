using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Programs;

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
