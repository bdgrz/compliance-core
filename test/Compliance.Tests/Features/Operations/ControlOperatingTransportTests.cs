using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Operations;
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

namespace Bdgrz.Compliance.Tests.Features.Operations;

[Trait("Category", "WebIntegration")]
public sealed class ControlOperatingTransportTests
{
    const string Base = "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}";

    [Fact]
    public async Task ShouldDescribePlanAndOccurrenceRoutesAsIdentifiedPutOperationsGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var routes = new[]
        {
            Base + "/operating-plan/proposals/{plan_version_id}",
            Base + "/operating-plan/proposals/{plan_version_id}/approvals/{command_id}",
            Base + "/occurrences/{command_id}",
            Base + "/occurrences/{occurrence_id}/attestations/{command_id}",
            Base + "/occurrences/{occurrence_id}/corrections/{command_id}",
            Base + "/occurrences/{occurrence_id}/reviews/{command_id}",
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
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _), route);
            Assert.False(operation.GetProperty("responses").TryGetProperty("202", out _), route);
        }

        foreach (var retired in new[] { Base + "/occurrences", Base + "/operating-plan/proposals" })
            Assert.False(paths.TryGetProperty(retired, out var item) && item.TryGetProperty("post", out _), retired);
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenPlanRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var owner = factory.CreateClient();
        using var approver = factory.CreateClient();
        var fixture = await ArrangeAsync(factory, owner, approver);
        var plan = Guid.NewGuid();
        var proposalPath = $"{fixture.ControlPath}/operating-plan/proposals/{plan}";

        // Act
        using var proposed = await owner.PutAsync(proposalPath, Json(fixture.Proposal(0, "Owner runs it monthly.")));
        var proposedText = await proposed.Content.ReadAsStringAsync();
        using var proposedAgain = await owner.PutAsync(proposalPath, Json(fixture.Proposal(0, "Owner runs it monthly.")));
        using var changedProposal = await owner.PutAsync(proposalPath, Json(fixture.Proposal(0, "A changed rationale.")));
        var approvalPath = $"{proposalPath}/approvals/{Guid.NewGuid()}";
        using var approved = await approver.PutAsync(approvalPath,
            Json("""{"expected_revision":1,"rationale":"Independent approval."}"""));
        var approvedText = await approved.Content.ReadAsStringAsync();
        using var approvedAgain = await approver.PutAsync(approvalPath,
            Json("""{"expected_revision":1,"rationale":"Independent approval."}"""));
        using var changedApproval = await approver.PutAsync(approvalPath,
            Json("""{"expected_revision":1,"rationale":"A changed rationale."}"""));
        using var proposedAfterApproval = await owner.PutAsync(proposalPath, Json(fixture.Proposal(0, "Owner runs it monthly.")));

        // Assert
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, proposedAgain.StatusCode);
        Assert.Equal(proposedText, await proposedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedProposal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approvedAgain.StatusCode);
        Assert.Equal(approvedText, await approvedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedApproval.StatusCode);
        Assert.Equal(proposedText, await proposedAfterApproval.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(approvedText);
        Assert.Equal("approved", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(plan.ToString(), document.RootElement.GetProperty("plan_version_id").GetString());
        await AssertFrozenResponseAsync(factory, fixture, nameof(ProposeControlOperatingPlan), proposed);
        await AssertFrozenResponseAsync(factory, fixture, nameof(ApproveControlOperatingPlan), approved);
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenOccurrenceRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var owner = factory.CreateClient();
        using var reviewer = factory.CreateClient();
        var fixture = await ArrangeAsync(factory, owner, reviewer);
        var plan = Guid.NewGuid();
        using var proposed = await owner.PutAsync($"{fixture.ControlPath}/operating-plan/proposals/{plan}",
            Json(fixture.Proposal(0, "Owner runs it monthly.")));
        using var approved = await reviewer.PutAsync(
            $"{fixture.ControlPath}/operating-plan/proposals/{plan}/approvals/{Guid.NewGuid()}",
            Json("""{"expected_revision":1,"rationale":"Independent approval."}"""));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var missed = await owner.GetAsync($"{fixture.ControlPath}/occurrences?state=missed");
        var occurrence = JsonDocument.Parse(await missed.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items")[0].GetProperty("occurrence_id").GetString()!;
        var occurrencePath = $"{fixture.ControlPath}/occurrences/{occurrence}";
        var attestPath = $"{occurrencePath}/attestations/{Guid.NewGuid()}";
        const string attestation = """{"expected_revision":0,"result":"failed","performed_at":"2026-01-01T00:00:00Z","notes":"Started.","rationale":"The export tool was unavailable.","evidence":[]}""";

        // Act
        using var attested = await owner.PutAsync(attestPath, Json(attestation));
        var attestedText = await attested.Content.ReadAsStringAsync();
        using var attestedAgain = await owner.PutAsync(attestPath, Json(attestation));
        using var changedAttestation = await owner.PutAsync(attestPath,
            Json(attestation.Replace("Started.", "Restarted.", StringComparison.Ordinal)));
        var attestationId = JsonDocument.Parse(attestedText).RootElement.GetProperty("attestations")[0]
            .GetProperty("attestation_id").GetString();
        var reviewPath = $"{occurrencePath}/reviews/{Guid.NewGuid()}";
        var review = $$"""{"expected_revision":2,"attestation_id":"{{attestationId}}","outcome":"returned","rationale":"Add support."}""";
        using var reviewed = await reviewer.PutAsync(reviewPath, Json(review));
        var reviewedText = await reviewed.Content.ReadAsStringAsync();
        using var reviewedAgain = await reviewer.PutAsync(reviewPath, Json(review));
        using var changedReview = await reviewer.PutAsync(reviewPath,
            Json(review.Replace("Add support.", "Other.", StringComparison.Ordinal)));
        var correctPath = $"{occurrencePath}/corrections/{Guid.NewGuid()}";
        const string correction = """{"expected_revision":3,"result":"complete","performed_at":"2026-01-01T00:00:00Z","notes":"Done.","evidence":[{"expected_evidence_index":0,"kind":"external","reference":"https://tickets.example/REV-1","description":"Signed review"}],"correction_reason":"Support attached."}""";
        using var corrected = await owner.PutAsync(correctPath, Json(correction));
        var correctedText = await corrected.Content.ReadAsStringAsync();
        using var correctedAgain = await owner.PutAsync(correctPath, Json(correction));
        using var changedCorrection = await owner.PutAsync(correctPath,
            Json(correction.Replace("Support attached.", "Other.", StringComparison.Ordinal)));

        // Assert
        Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
        Assert.Equal(attestedText, await attestedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedAttestation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal(reviewedText, await reviewedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedReview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.Equal(correctedText, await correctedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedCorrection.StatusCode);
        Assert.Equal("submitted", JsonDocument.Parse(correctedText).RootElement.GetProperty("state").GetString());
        Assert.Equal("returned", JsonDocument.Parse(reviewedText).RootElement.GetProperty("state").GetString());
        var occurrenceId = Uuid.Parse(occurrence, CultureInfo.InvariantCulture);
        await AssertFrozenResponseAsync(factory, fixture, nameof(AttestControlOccurrence), attested, occurrenceId);
        await AssertFrozenResponseAsync(factory, fixture, nameof(ReviewControlOccurrence), reviewed, occurrenceId);
        await AssertFrozenResponseAsync(factory, fixture, nameof(CorrectControlAttestation), corrected, occurrenceId);
    }

    [Fact]
    public async Task ShouldOpenOneOccurrencePerBusinessWindowGivenOpenRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var owner = factory.CreateClient();
        using var approver = factory.CreateClient();
        var fixture = await ArrangeAsync(factory, owner, approver);
        var plan = Guid.NewGuid();
        using var proposed = await owner.PutAsync($"{fixture.ControlPath}/operating-plan/proposals/{plan}",
            Json(fixture.Proposal(0, "Owner opens work when triggered.", adHoc: true)));
        using var approved = await approver.PutAsync(
            $"{fixture.ControlPath}/operating-plan/proposals/{plan}/approvals/{Guid.NewGuid()}",
            Json("""{"expected_revision":1,"rationale":"Independent approval."}"""));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var open = JsonSerializer.Serialize(new
        {
            trigger = "Release 4.2",
            occurred_on = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            expected_revision = 0,
            business_window = new { kind = "event", source_kind = "release", source_id = "4.2" },
        });
        var path = $"{fixture.ControlPath}/occurrences/{Guid.NewGuid()}";

        // Act
        using var first = await owner.PutAsync(path, Json(open));
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await owner.PutAsync(path, Json(open));
        using var secondCommand = await owner.PutAsync($"{fixture.ControlPath}/occurrences/{Guid.NewGuid()}", Json(open));
        using var list = await owner.GetAsync($"{fixture.ControlPath}/occurrences");

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(firstText, await retried.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, secondCommand.StatusCode);
        var items = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(JsonDocument.Parse(firstText).RootElement.GetProperty("occurrence_id").GetString(),
            items[0].GetProperty("occurrence_id").GetString());
    }

    [Fact]
    public async Task ShouldRequireCurrentGrantBeforeFrozenReplayGivenProductionOperatingPlanRoute()
    {
        // Arrange
        var permissions = new MutablePermissions();
        await using var factory = Factory(permissions);
        using var owner = factory.CreateClient();
        using var approver = factory.CreateClient();
        var fixture = await ArrangeAsync(factory, owner, approver);
        var route = $"{fixture.ControlPath}/operating-plan/proposals/{Uuid.CreateVersion4()}";
        var json = fixture.Proposal(0, "Original scoped proposal.");
        using var first = await owner.PutAsync(route, Json(json));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertFrozenResponseAsync(factory, fixture, nameof(ProposeControlOperatingPlan), first);
        permissions.Allowed = false;

        // Act
        using var denied = await owner.PutAsync(route, Json(json));
        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.PutAsync(route, Json(json));
        await using var scope = factory.Services.CreateAsyncScope();
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(1, retained.Revision);
    }

    static async Task AssertFrozenResponseAsync(WebApplicationFactory<Program> factory, Arranged fixture,
        string commandKind, HttpResponseMessage response, Uuid? occurrenceId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var stream = occurrenceId is { } id
            ? new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, id).Stream
            : new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId).Stream;
        var matches = new List<INativeOperationFact>();
        await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit && record.Event is INativeOperationFact native && native.Operation.CommandKind == commandKind)
                matches.Add(native);
        var fact = Assert.Single(matches);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var descriptor = await FirstResponses.ReplayAsync(content, fixture.TenantId.ToString(), fact.FirstResponse!);
        var frozen = await content.ReadVerifiedAsync(fixture.TenantId.ToString(), Assert.IsType<StandardResponseBody>(descriptor.Body).Content);
        Assert.Equal(200, descriptor.HttpStatus);
        Assert.Equal(descriptor.HttpStatus, (int)response.StatusCode);
        Assert.Equal(frozen.ToArray(), await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(descriptor.ContentType, response.Content.Headers.ContentType?.ToString());
    }

    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    static async Task<Arranged> ArrangeAsync(WebApplicationFactory<Program> factory, HttpClient owner,
        HttpClient other)
    {
        var ownerUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(owner,
            $"operating-owner-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var otherUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(other,
            $"operating-other-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var (tenant, program) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var controlId = ControlDraft.IdFor(tenant, program, "AC-E2E");
        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60);
        var ownerMember = RbacIds.Member(tenant, ownerUser);
        var otherMember = RbacIds.Member(tenant, otherUser);
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, ownerUser), member => member.Register());
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, otherUser), member => member.Register());
        await ControlOperationE2ETests.SeedApprovedControlAsync(factory, tenant, program, controlId, ownerMember,
            effectiveFrom);
        return new Arranged($"/api/v1/tenants/{tenant}/programs/{program}/controls/{controlId}", tenant, program, controlId,
            ownerMember, otherMember, effectiveFrom);
    }

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory(IPermissionAuthorizer? permissions = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "control-operating-tests");
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
                    new PermissionBackedAccessGrantPermissionAuthorizer(permissions ?? new RecordingPermissionAuthorizer(true)));
                services.RemoveAll<IProgramResourceScopeResolver>();
                services.AddSingleton(ProgramManagementServices.ResourceScopes());
            });
        });

    sealed record Arranged(string ControlPath, Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid OwnerMember, Uuid ReviewerMember,
        DateOnly EffectiveFrom)
    {
        public string Proposal(long expectedRevision, string rationale, bool adHoc = false)
        {
            object cadence = adHoc
                ? new { kind = "ad_hoc", due_within_days = 10 }
                : new
                {
                    kind = "recurring",
                    frequency = "monthly",
                    first_period_start = EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    due_within_days = 5,
                };
            return JsonSerializer.Serialize(new
            {
                expected_revision = expectedRevision,
                control_version_id = ControlVersionIds.Initial(ControlId).ToString(),
                owner = new { kind = "member", id = OwnerMember.ToString() },
                reviewer_member_id = ReviewerMember.ToString(),
                cadence,
                effective_from = EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rationale,
            });
        }
    }
}
