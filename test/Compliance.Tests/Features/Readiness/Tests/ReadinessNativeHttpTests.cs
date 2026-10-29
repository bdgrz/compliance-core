using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

[Trait("Category", "WebIntegration")]
public sealed class ReadinessNativeHttpTests
{
    [Fact]
    public async Task ShouldReplayOriginal200PlanBytesAndRequireCurrentPermissionGivenLaterPlan()
    {
        // Arrange
        await using var source = ReadinessTestServices.Build();
        await using var scope = source.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IEventStore>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var assessment = Uuid.CreateVersion4();
        var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed", "Review risks.", []);
        await SeedProgramAsync(source, tenant, program);
        await ReadinessSeed.AdoptAsync(reader, services.GetRequiredService<IAggregateExecutor>(), content, tenant,
            program, assessment, new ReadinessEvaluation([], [], [gap], new string('a', 64)), DateTimeOffset.UtcNow,
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow);
        var permissions = new MutablePermissions();
        await using var factory = Factory(events, content, permissions);
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "readiness-plan-native@example.test"), CultureInfo.InvariantCulture);
        await ProgramManagementServices.SeedAsync(source, new Member(tenant, user), member => member.Register());
        var request = new PlanReadinessGap(Uuid.CreateVersion4(), tenant, program, gap.GapId, 0,
            RbacIds.Member(tenant, user), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "Original plan.");
        var prefix = $"/api/v1/tenants/{tenant}/programs/{program}/readiness/gaps/{gap.GapId}/plan-readiness-gap";
        var route = $"{prefix}/{request.CommandId}";
        using var first = await PutAsync(client, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.PlanReadinessGap));
        var original = await first.Content.ReadAsByteArrayAsync();
        var later = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 1, Action = "Later plan." };
        using var advanced = await PutAsync(client, $"{prefix}/{later.CommandId}", JsonSerializer.Serialize(later, ComplianceCoreJsonContext.Default.PlanReadinessGap));

        // Act
        using var replay = await PutAsync(client, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.PlanReadinessGap));
        using var changed = await PutAsync(client, route, JsonSerializer.Serialize(request with { Action = "Changed intent." }, ComplianceCoreJsonContext.Default.PlanReadinessGap));
        permissions.Allowed = false;
        using var denied = await PutAsync(client, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.PlanReadinessGap));
        using var anonymous = factory.CreateClient();
        using var unauthenticated = await PutAsync(anonymous, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.PlanReadinessGap));
        var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenant, program, gap.GapId, request.CommandId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(2, owner.Revision);
        var descriptor = await FirstResponses.ReplayAsync(content, tenant.ToString(), owner.Acceptance.SelectedOperation!.FirstResponse!);
        var stored = await content.ReadVerifiedAsync(tenant.ToString(), Assert.IsType<StandardResponseBody>(descriptor.Body).Content);
        Assert.Equal(stored.ToArray(), original);
        Assert.Equal(200, descriptor.HttpStatus);
    }

    [Fact]
    public async Task ShouldReplayOriginal202BytesGivenCompletedRunAndLaterAdoption()
    {
        // Arrange
        await using var source = ReadinessTestServices.Build();
        await using var scope = source.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IEventStore>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        await SeedProgramAsync(source, tenant, program);
        var permissions = new MutablePermissions();
        await using var factory = Factory(events, content, permissions);
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "readiness-run-native@example.test"), CultureInfo.InvariantCulture);
        await ProgramManagementServices.SeedAsync(source, new Member(tenant, user), member => member.Register());
        var request = new RunReadinessAssessment(Uuid.CreateVersion4(), tenant, program, 0);
        var route = $"/api/v1/tenants/{tenant}/programs/{program}/readiness/assessments/{request.AssessmentId}";
        using var first = await PutAsync(client, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.RunReadinessAssessment));
        var original = await first.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var completed = await ReadinessProcesses.RunRunAsync(source, tenant, program, request.AssessmentId);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.Processes.ProcessState.Completed, completed.State);
        await ReadinessSeed.AdoptAsync(reader, services.GetRequiredService<IAggregateExecutor>(), content, tenant,
            program, Uuid.CreateVersion4(), new ReadinessEvaluation([], [], [], new string('a', 64)), DateTimeOffset.UtcNow,
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow);
        var before = await reader.HydrateAsync(new ProgramReadiness(tenant, program, request.AssessmentId));

        // Act
        using var replay = await PutAsync(client, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.RunReadinessAssessment));
        permissions.Allowed = false;
        using var denied = await PutAsync(client, route, JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.RunReadinessAssessment));
        var after = await reader.HydrateAsync(new ProgramReadiness(tenant, program, request.AssessmentId));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal(first.Headers.Location, replay.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        var descriptor = await FirstResponses.ReplayAsync(content, tenant.ToString(), after.Acceptance.SelectedOperation!.FirstResponse!);
        var stored = await content.ReadVerifiedAsync(tenant.ToString(), Assert.IsType<StandardResponseBody>(descriptor.Body).Content);
        Assert.Equal(stored.ToArray(), original);
        Assert.Equal(202, descriptor.HttpStatus);
    }

    static Task SeedProgramAsync(IServiceProvider provider, Uuid tenant, Uuid program) =>
        ProgramManagementServices.SeedAsync(provider, new ProgramRules(tenant, program), owner =>
        {
            Assert.Null(owner.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
                Uuid.CreateVersion4(), "Program author", DateTimeOffset.UtcNow));
            return Result.Success;
        });

    static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program> Factory(IEventStore events,
        IImmutableContentStore content, MutablePermissions permissions) => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IDomainEventReader>();
            services.RemoveAll<IImmutableContentStore>();
            services.AddSingleton(events);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
            services.AddSingleton(content);
            services.AddSingleton<IReadinessSourceReader>(new EmptySources());
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(permissions);
            services.RemoveAll<IAccessGrantPermissionAuthorizer>();
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(permissions));
        }));

    static async Task<HttpResponseMessage> PutAsync(HttpClient client, string route, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync(route, body);
    }

    sealed class EmptySources : IReadinessSourceReader
    {
        public ValueTask<Result<ReadinessSourceSet>> ReadAsync(Uuid tenantId, Uuid programId, DateTimeOffset asOf,
            CancellationToken ct = default) => ValueTask.FromResult(Result<ReadinessSourceSet>.Success(ReadinessSourceSet.Empty));
    }

    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}
