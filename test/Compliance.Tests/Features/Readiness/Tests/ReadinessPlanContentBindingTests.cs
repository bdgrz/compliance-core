using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessPlanContentBindingTests
{
    [Theory]
    [InlineData("gap")]
    [InlineData("actor")]
    [InlineData("time")]
    [InlineData("revision")]
    public async Task ShouldRejectValidImmutablePlanBodyGivenAnotherNativeOwnerOrOriginalAttribution(string mismatch)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var gap = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var at = DateTimeOffset.UtcNow;
        var member = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(member, "Planner");
        var plan = new ReadinessGapPlan(tenant, program, gap, command);
        var operation = ReadinessContent.Provenance(command, nameof(PlanReadinessGap), plan.Stream, actor,
            0, new string('a', 64), at);
        var view = new ReadinessGapPlanView(gap, member, DateOnly.FromDateTime(at.UtcDateTime).AddDays(30),
            "Complete original body.", actor, at)
        { Revision = 1 };
        view = mismatch switch
        {
            "gap" => view with { GapId = Uuid.CreateVersion4() },
            "actor" => view with { PlannedBy = ActorReference.ForMember(Uuid.CreateVersion4(), "Other planner") },
            "time" => view with { PlannedAt = at.AddMinutes(1) },
            "revision" => view with { Revision = 9 },
            _ => view,
        };
        var body = await ReadinessContent.WriteAsync(content, tenant, ReadinessContent.PlanSchema, view, CancellationToken.None);
        var frozen = await ReadinessContent.FreezeAsync(content, tenant, ReadinessContent.PlanViewSchema, view, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), frozen.Response);
        var stored = await services.GetRequiredService<IAggregateExecutor>().ExecuteAsync(plan,
            owner => AggregateOutcome.Commit(owner.Plan(operation, body, first, frozen.Result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(stored.IsSuccess, stored.Error?.Message);
        var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenant, program, gap));
        var readiness = new ReadinessReader(reader, content, services.GetRequiredService<IDomainEventReader>());

        // Act
        var failure = await Record.ExceptionAsync(async () => await readiness.ReadPlanAsync(tenant, owner));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(view, await content.ReadJsonAsync<ReadinessGapPlanView>(tenant.ToString(), body));
        Assert.Equal(1UL, owner.Version);
    }
}
