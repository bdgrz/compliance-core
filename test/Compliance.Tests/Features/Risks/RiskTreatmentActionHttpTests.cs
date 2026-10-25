using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

[Trait("Category", "WebIntegration")]
public sealed class RiskTreatmentActionHttpTests
{
    [Fact]
    public async Task ShouldReturnOriginal200BytesAndRequireCurrentGrantGivenActionRetryAfterLaterRevision()
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = source.Provider;
        await SeedProgramAsync(source);
        await using var scope = provider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var permissions = new MutablePermissions();
        await using var factory = Factory(events, content, permissions);
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "action-native-http@example.test"), CultureInfo.InvariantCulture);
        await ProgramManagementServices.SeedAsync(provider, new Member(source.TenantId, user), member => member.Register());
        var worker = await source.SeedMemberAsync();
        var creation = source.AddAction(worker, [], 0);
        var prefix = $"/api/v1/tenants/{source.TenantId}/programs/{source.ProgramId}/risks/{source.RiskId}/treatment-actions";
        using var first = await PutAsync(client, $"{prefix}/{creation.ActionId}", JsonSerializer.Serialize(creation,
            ComplianceCoreJsonContext.Default.AddRiskTreatmentAction));
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        var revision = new ReviseRiskTreatmentAction(Uuid.CreateVersion4(), source.TenantId, source.ProgramId,
            source.RiskId, creation.ActionId, 1, "First revision", "Hardware keys", "Provider export",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), worker.MemberId, []);
        var route = $"{prefix}/revise-risk-treatment-action/{revision.CommandId}";
        using var revised = await PutAsync(client, route, JsonSerializer.Serialize(revision,
            ComplianceCoreJsonContext.Default.ReviseRiskTreatmentAction));
        var later = revision with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, Title = "Later revision" };
        using var advanced = await PutAsync(client, $"{prefix}/revise-risk-treatment-action/{later.CommandId}",
            JsonSerializer.Serialize(later, ComplianceCoreJsonContext.Default.ReviseRiskTreatmentAction));

        // Act
        using var creationRetry = await PutAsync(client, $"{prefix}/{creation.ActionId}", JsonSerializer.Serialize(creation,
            ComplianceCoreJsonContext.Default.AddRiskTreatmentAction));
        using var revisionRetry = await PutAsync(client, route, JsonSerializer.Serialize(revision,
            ComplianceCoreJsonContext.Default.ReviseRiskTreatmentAction));
        using var changed = await PutAsync(client, route, JsonSerializer.Serialize(revision with { Title = "Changed intent" },
            ComplianceCoreJsonContext.Default.ReviseRiskTreatmentAction));
        permissions.Allowed = false;
        using var denied = await PutAsync(client, route, JsonSerializer.Serialize(revision,
            ComplianceCoreJsonContext.Default.ReviseRiskTreatmentAction));
        using var anonymous = factory.CreateClient();
        using var unauthenticated = await PutAsync(anonymous, route, JsonSerializer.Serialize(revision,
            ComplianceCoreJsonContext.Default.ReviseRiskTreatmentAction));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        Assert.Equal(HttpStatusCode.OK, creationRetry.StatusCode);
        Assert.Equal(firstBytes, await creationRetry.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, revisionRetry.StatusCode);
        Assert.Equal("{}", await revised.Content.ReadAsStringAsync());
        Assert.Equal("{}", await revisionRetry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(3, (await source.ActionAsync(creation.ActionId)).Revision);
    }

    [Fact]
    public async Task ShouldReplayOriginal202BytesGivenHistoricalReviewAfterDecisionAndNewSubmission()
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = source.Provider;
        await SeedProgramAsync(source);
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var permissions = new MutablePermissions();
        await using var factory = Factory(scope.ServiceProvider.GetRequiredService<IEventStore>(), content, permissions);
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "action-review-http@example.test"), CultureInfo.InvariantCulture);
        await ProgramManagementServices.SeedAsync(provider, new Member(source.TenantId, user), member => member.Register());
        var worker = await source.SeedMemberAsync();
        var evidence = await source.SeedEvidenceAsync(true, worker);
        var added = await source.Scenario(source.AssessorUserId).When(source.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var submitted = await PersonalRiskSignoffTransportTests.HttpAsync(provider, source.AssessorUserId,
            source.Submit(added.Value.ActionId, 1, [evidence]));
        await source.AssignReviewAsync(submitted.Value.SubmissionId, user);
        var review = source.ReviewAction(added.Value.ActionId, 2, RiskStates.Reject);
        var route = $"/api/v1/tenants/{source.TenantId}/programs/{source.ProgramId}/risks/{source.RiskId}/treatment-actions/{review.ActionId}/completion-reviews/{review.CommandId}";
        var json = JsonSerializer.Serialize(review, ComplianceCoreJsonContext.Default.ReviewRiskTreatmentActionCompletion);
        using var accepted = await PutAsync(client, route, json);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var bytes = await accepted.Content.ReadAsByteArrayAsync();
        var admitted = (await source.ActionAsync(review.ActionId)).PendingReview!;
        var frozen = await FirstResponses.ReplayAsync(content, source.TenantId.ToString(), admitted.FirstResponse!);
        var storedBytes = await content.ReadVerifiedAsync(source.TenantId.ToString(), Assert.IsType<StandardResponseBody>(frozen.Body).Content);
        Assert.Equal(ProcessState.Completed, (await RiskActionProcesses.RunAsync(provider, source.TenantId, review.CommandId)).State);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, source.AssessorUserId,
            source.Submit(review.ActionId, 4, [evidence]));
        var before = await source.ActionAsync(review.ActionId);

        // Act
        using var replay = await PutAsync(client, route, json);
        permissions.Allowed = false;
        using var denied = await PutAsync(client, route, json);

        // Assert
        Assert.Equal(202, frozen.HttpStatus);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(bytes, storedBytes.ToArray());
        Assert.Equal(bytes, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal(frozen.ContentType, replay.Content.Headers.ContentType?.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(before.CommittedStreamPosition, (await source.ActionAsync(review.ActionId)).CommittedStreamPosition);
    }

    static Task SeedProgramAsync(RiskGovernanceHandlerTests.Fixture fixture) =>
        ProgramManagementServices.SeedAsync(fixture.Provider, new ProgramRules(fixture.TenantId, fixture.ProgramId),
            program =>
            {
                Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
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

    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}
