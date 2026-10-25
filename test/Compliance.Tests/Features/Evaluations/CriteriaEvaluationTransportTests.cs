using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

/// <summary>
///     The twelve criteria, applicability, mapping and evaluation commands as identified 200 PUT operations: an exact
///     retry returns the original body, a changed intent under the same identity conflicts, and the retired POST
///     routes are gone.
/// </summary>
[Trait("Category", "WebIntegration")]
public sealed class CriteriaEvaluationTransportTests
{
    const string ProgramBase = "/api/v1/tenants/{tenant_id}/programs/{program_id}";
    static readonly Guid EditionId = CriteriaCatalog.Platform.Edition.EditionId.ToGuid();

    [Fact]
    public async Task ShouldDescribeEveryRouteAsAnIdentifiedPutOperationGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var routes = new[]
        {
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id}",
            ProgramBase + "/control-mappings/{mapping_id}",
            ProgramBase + "/control-mappings/{mapping_id}/reviews/{command_id}",
            ProgramBase + "/control-mappings/{mapping_id}/retirements/{command_id}",
            ProgramBase + "/criterion-applicability/{decision_id}",
            ProgramBase + "/criterion-applicability/{decision_id}/reviews/{command_id}",
            ProgramBase + "/criterion-applicability/{decision_id}/withdrawals/{command_id}",
            ProgramBase + "/controls/{control_id}/evaluations/{evaluation_id}",
            ProgramBase + "/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id}",
            ProgramBase + "/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id}",
            ProgramBase + "/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id}",
            ProgramBase + "/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id}",
        };

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var route in routes)
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.TryGetProperty("requestBody", out _), route);
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _),
                route);
            Assert.False(operation.GetProperty("responses").TryGetProperty("202", out _), route);
        }

        foreach (var retired in new[]
                 {
                     ProgramBase + "/control-mappings", ProgramBase + "/criterion-applicability",
                     ProgramBase + "/controls/{control_id}/evaluations",
                     ProgramBase + "/control-mappings/{mapping_id}/reviews",
                     ProgramBase + "/criterion-applicability/{decision_id}/reviews",
                 })
            Assert.False(paths.TryGetProperty(retired, out var item) && item.TryGetProperty("post", out _), retired);
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenOverlayRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var editor = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(editor,
            $"overlay-editor-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        Assert.NotEqual(Uuid.Empty, user);
        var tenant = Guid.NewGuid();
        var path = $"/api/v1/tenants/{tenant}/criteria-editions/{EditionId}/overlays/set-criteria-text-overlay/{Guid.NewGuid()}";
        const string body = """{"identifier":"CC6.1","expected_revision":0,"content":{"text":"Supplied wording.","supplier":"Supplier","license_reference":"License 1","usage_flags":{"display":true,"export":false}}}""";

        // Act
        using var first = await editor.PutAsync(path, Json(body));
        var firstText = await first.Content.ReadAsStringAsync();
        using var again = await editor.PutAsync(path, Json(body));
        using var changed = await editor.PutAsync(path, Json(body.Replace("Supplied wording.", "Other wording.",
            StringComparison.Ordinal)));
        using var stale = await editor.PutAsync(
            $"/api/v1/tenants/{tenant}/criteria-editions/{EditionId}/overlays/set-criteria-text-overlay/{Guid.NewGuid()}",
            Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(firstText, await again.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(1, document.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal("CC6.1", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenMappingAndApplicabilityRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var author = factory.CreateClient();
        using var reviewer = factory.CreateClient();
        var arranged = await ArrangeAsync(factory, author, reviewer, selectEdition: true);
        var mappingId = Guid.NewGuid();
        var mappingPath = $"{arranged.ProgramPath}/control-mappings/{mappingId}";
        var mapping = $$"""{"control_id":"{{arranged.ControlId}}","control_version_id":"{{ControlVersionIds.Initial(arranged.ControlId)}}","edition_id":"{{EditionId}}","criterion_identifier":"CC6.1","expected_revision":0,"rationale":"Access reviews restrict access.","applicability_explanation":"All production systems."}""";
        var decisionId = Guid.NewGuid();
        var decisionPath = $"{arranged.ProgramPath}/criterion-applicability/{decisionId}";
        var decision = $$"""{"edition_id":"{{EditionId}}","criterion_identifier":"CC6.2","expected_revision":0,"rationale":"No accounts are approved."}""";

        // Act
        using var proposed = await author.PutAsync(mappingPath, Json(mapping));
        var proposedText = await proposed.Content.ReadAsStringAsync();
        using var proposedAgain = await author.PutAsync(mappingPath, Json(mapping));
        using var changedProposal = await author.PutAsync(mappingPath, Json(mapping.Replace("restrict access.",
            "restrict everything.", StringComparison.Ordinal)));
        var reviewPath = $"{mappingPath}/reviews/{Guid.NewGuid()}";
        const string review = """{"expected_revision":1,"outcome":"accept","rationale":"The rationale matches the criterion."}""";
        using var reviewed = await reviewer.PutAsync(reviewPath, Json(review));
        using var reviewedAgain = await reviewer.PutAsync(reviewPath, Json(review));
        using var changedReview = await reviewer.PutAsync(reviewPath, Json(review.Replace("matches", "differs",
            StringComparison.Ordinal)));
        using var proposedAfterReview = await author.PutAsync(mappingPath, Json(mapping));
        using var decided = await author.PutAsync(decisionPath, Json(decision));
        var decidedText = await decided.Content.ReadAsStringAsync();
        using var decidedAgain = await author.PutAsync(decisionPath, Json(decision));
        using var changedDecision = await author.PutAsync(decisionPath, Json(decision.Replace("approved",
            "allowed", StringComparison.Ordinal)));
        var decisionReviewPath = $"{decisionPath}/reviews/{Guid.NewGuid()}";
        using var decisionReviewed = await reviewer.PutAsync(decisionReviewPath, Json(review));
        using var withdrawn = await author.PutAsync($"{decisionPath}/withdrawals/{Guid.NewGuid()}",
            Json("""{"expected_revision":2,"rationale":"The service now approves accounts."}"""));
        using var retired = await reviewer.PutAsync($"{mappingPath}/retirements/{Guid.NewGuid()}",
            Json("""{"expected_revision":2,"rationale":"The control no longer addresses access."}"""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        Assert.Equal(proposedText, await proposedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedProposal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changedReview.StatusCode);
        Assert.Equal(proposedText, await proposedAfterReview.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        Assert.Equal(decidedText, await decidedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedDecision.StatusCode);
        Assert.Equal(HttpStatusCode.OK, decisionReviewed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        using var registration = JsonDocument.Parse(proposedText);
        Assert.Equal(mappingId.ToString(), registration.RootElement.GetProperty("mapping_id").GetString());
        Assert.Equal(1, registration.RootElement.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenEvaluationRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var owner = factory.CreateClient();
        using var reviewer = factory.CreateClient();
        var arranged = await ArrangeAsync(factory, owner, reviewer, selectEdition: false);
        var steps = """[{"assertion":"design","method":"inspection","inspected_items":[{"kind":"policy","reference":"policy/access-review","version":"v3"}],"expected_condition":"Policy requires a review."},{"assertion":"implementation","method":"reperformance","inspected_items":[{"kind":"artifact","reference":"exports/q3.csv","version":"sha256:ab12"}],"expected_condition":"Every leaver was removed."},{"assertion":"evidence_sufficiency","method":"inspection","inspected_items":[{"kind":"artifact","reference":"exports/q3.csv","version":"sha256:ab12"}],"expected_condition":"The export is complete."}]""";
        using var plan = await owner.PostAsync($"{arranged.ControlPath}/evaluation-plan/versions", Json(
            $$"""{"control_version_id":"{{ControlVersionIds.Initial(arranged.ControlId)}}","expected_version":0,"objective":"Evaluate the control.","steps":{{steps}},"tester_independence_required":false}"""));
        Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        var planVersion = JsonDocument.Parse(await plan.Content.ReadAsStringAsync()).RootElement
            .GetProperty("plan_version_id").GetString();
        var evaluationId = Guid.NewGuid();
        var evaluationPath = $"{arranged.ControlPath}/evaluations/{evaluationId}";
        var start = $$"""{"plan_version_id":"{{planVersion}}"}""";

        // Act
        using var started = await owner.PutAsync(evaluationPath, Json(start));
        var startedText = await started.Content.ReadAsStringAsync();
        using var startedAgain = await owner.PutAsync(evaluationPath, Json(start));
        using var changedStart = await owner.PutAsync(evaluationPath,
            Json($$"""{"plan_version_id":"{{planVersion}}","retest_of_evaluation_id":"{{Guid.NewGuid()}}"}"""));
        var stepIds = JsonDocument.Parse(startedText).RootElement.GetProperty("steps").EnumerateArray()
            .Select(static step => step.GetProperty("step_id").GetString()!).ToArray();
        var texts = new List<string>();
        var first = $$"""{"expected_revision":1,"result":"met","rationale":"Inspected the exact items.","inspected_items":[{"kind":"policy","reference":"policy/access-review","version":"v3"}]}""";
        var firstStepPath = $"{evaluationPath}/steps/{stepIds[0]}/results/{Guid.NewGuid()}";
        using var recorded = await owner.PutAsync(firstStepPath, Json(first));
        texts.Add(await recorded.Content.ReadAsStringAsync());
        using var recordedAgain = await owner.PutAsync(firstStepPath, Json(first));
        using var changedStep = await owner.PutAsync(firstStepPath, Json(first.Replace("exact", "other",
            StringComparison.Ordinal)));
        var revision = 2;
        foreach (var index in new[] { 1, 2 })
        {
            using var step = await owner.PutAsync($"{evaluationPath}/steps/{stepIds[index]}/results/{Guid.NewGuid()}",
                Json($$"""{"expected_revision":{{revision}},"result":"met","rationale":"Inspected.","inspected_items":[{"kind":"artifact","reference":"exports/q3.csv","version":"sha256:ab12"}]}"""));
            Assert.Equal(HttpStatusCode.OK, step.StatusCode);
            revision++;
        }

        var submitPath = $"{evaluationPath}/submissions/{Guid.NewGuid()}";
        var submit = $$"""{"expected_revision":{{revision}},"conclusions":[{"assertion":"design","conclusion":"effective","rationale":"Design."},{"assertion":"implementation","conclusion":"effective","rationale":"Implementation."},{"assertion":"evidence_sufficiency","conclusion":"effective","rationale":"Evidence."}]}""";
        using var submitted = await owner.PutAsync(submitPath, Json(submit));
        var submittedText = await submitted.Content.ReadAsStringAsync();
        using var submittedAgain = await owner.PutAsync(submitPath, Json(submit));
        using var changedSubmit = await owner.PutAsync(submitPath, Json(submit.Replace("Design.", "Other.",
            StringComparison.Ordinal)));
        var reviewPath = $"{evaluationPath}/reviews/{Guid.NewGuid()}";
        var review = $$"""{"expected_revision":{{revision + 1}},"decision":"accepted","rationale":"Independent review."}""";
        using var reviewed = await reviewer.PutAsync(reviewPath, Json(review));
        var reviewedText = await reviewed.Content.ReadAsStringAsync();
        using var reviewedAgain = await reviewer.PutAsync(reviewPath, Json(review));
        using var changedReview = await reviewer.PutAsync(reviewPath, Json(review.Replace("Independent",
            "Hasty", StringComparison.Ordinal)));
        using var startedLast = await owner.PutAsync(evaluationPath, Json(start));

        // Assert
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(startedText, await startedAgain.Content.ReadAsStringAsync());
        Assert.Equal(startedText, await startedLast.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedStart.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(texts[0], await recordedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedStep.StatusCode);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal(submittedText, await submittedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedSubmit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal(reviewedText, await reviewedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedReview.StatusCode);
        using var final = JsonDocument.Parse(reviewedText);
        Assert.Equal("accepted", final.RootElement.GetProperty("state").GetString());
        Assert.Equal(1, JsonDocument.Parse(startedText).RootElement.GetProperty("revision").GetInt64());
    }

    static async Task<Arranged> ArrangeAsync(WebApplicationFactory<Program> factory, HttpClient first, HttpClient second,
        bool selectEdition)
    {
        var firstUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(first,
            $"criteria-first-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var secondUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(second,
            $"criteria-second-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var (tenant, program) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var controlId = ControlDraft.IdFor(tenant, program, "AC-E2E");
        var firstMember = RbacIds.Member(tenant, firstUser);
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, firstUser), member => member.Register());
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, secondUser), member => member.Register());
        await ControlOperationE2ETests.SeedApprovedControlAsync(factory, tenant, program, controlId, firstMember,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60));
        if (selectEdition)
        {
            var admin = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            await ControlOperationE2ETests.SeedAsync(factory, new ProgramRules(tenant, program), item =>
                Command(item.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), admin, "Admin", now)));
            await ControlOperationE2ETests.SeedAsync(factory, new ProgramRules(tenant, program), item =>
                Command(item.SelectCriteriaEdition(1, CriteriaCatalog.Platform.Edition.EditionId, admin, "Admin", now)));
        }

        var programPath = $"/api/v1/tenants/{tenant}/programs/{program}";
        return new Arranged(programPath, $"{programPath}/controls/{controlId}", controlId);
    }

    static Result Command(Bdgrz.Compliance.Features.Versioning.CommandFailure? failure) => failure is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "criteria-evaluation-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(descriptor =>
                             descriptor.ServiceType == typeof(IHostedService) &&
                             descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                    services.Remove(descriptor);
                var store = new InMemoryEventStore();
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton<IEventStore>(store);
                services.AddSingleton<IDomainEventReader>(store);
                services.RemoveAll<IKvClient>();
                services.AddSingleton<IKvClient>(new InMemoryKvClient());
                services.RemoveAll<ITenantMembershipDirectoryReader>();
                services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                services.RemoveAll<ITenantActivity>();
                services.AddSingleton<ITenantActivity, ActiveTenant>();
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                    new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
                services.RemoveAll<IProgramResourceScopeResolver>();
                services.AddSingleton(ProgramManagementServices.ResourceScopes());
            });
        });

    sealed record Arranged(string ProgramPath, string ControlPath, Uuid ControlId);
}
