using System.Globalization;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

[Trait("Category", "WebIntegration")]
public sealed class EvaluationScopeNativeHttpTests
{
    [Fact]
    public async Task ShouldReturnFrozenPlanBytesGivenExistingPostContractWithDerivedIdentity()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tracked = new TrackedContent(source.Content);
        await using var factory = Factory(source, tracked);
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "plan-native@example.test"), CultureInfo.InvariantCulture);
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var control = ControlDraft.IdFor(tenant, program, "PLAN-NATIVE");
        await source.SeedProgramAsync(tenant, program, user);
        var ownerMember = Uuid.CreateVersion4();
        var rules = new ControlRules(tenant, control);
        Assert.True(rules.Create(program, Uuid.CreateVersion4(), "PLAN-NATIVE",
            new ControlDraftContent("Review", "Objective", "Control description", "Narrative", ["Signed record"]),
            Uuid.CreateVersion4(), "Author", source.Clock.Now).IsSuccess);
        Assert.Null(rules.AssignResponsibility(new ResponsibilityScope("control", control, rules.DraftVersionId, 1),
            Uuid.CreateVersion4(), ownerMember, ResponsibilityType.ControlOwner, Uuid.CreateVersion4(), "Manager",
            source.Clock.Now, source.Clock.Now, null, []));
        var review = Uuid.CreateVersion4();
        Assert.Null(rules.Review(program, 1, review, "accept", "Independent review", Uuid.CreateVersion4(), "Reviewer", source.Clock.Now));
        Assert.Null(rules.Approve(program, 1, Uuid.CreateVersion4(), review, DateOnly.FromDateTime(source.Clock.Now.UtcDateTime),
            "Approved", new HashSet<Uuid> { ownerMember }, Uuid.CreateVersion4(), "Approver", source.Clock.Now));
        await source.SeedAsync(rules);
        var version = Assert.Single(rules.ReadVersions()).VersionId;
        var route = $"/api/v1/tenants/{tenant}/programs/{program}/controls/{control}/evaluation-plan/versions";
        var steps = new[] { new EvaluationProcedureStep("design", "inspection",
            [new EvaluationInspectedItem("control", control.ToString(), version.ToString())], "The exact approved control is implemented.") };
        var body = new Dictionary<string, object>
        {
            ["control_version_id"] = version,
            ["expected_version"] = 0,
            ["objective"] = "Review exact original sources",
            ["steps"] = JsonSerializer.SerializeToElement(steps, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(IReadOnlyList<EvaluationProcedureStep>))!),
            ["tester_independence_required"] = false,
        };

        // Act
        using var first = await client.PostAsJsonAsync(route, body);
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        var firstDescriptorReads = tracked.DescriptorReads;
        using var retry = await client.PostAsJsonAsync(route, body);
        var owner = await source.Reader.HydrateAsync(new ControlEvaluationPlan(tenant, program, control));
        var fact = Assert.IsType<ControlEvaluationPlanVersionDefined>(owner.Latest);
        var descriptor = await FirstResponses.ReplayAsync(source.Content, tenant.ToString(), fact.FirstResponse!);
        var expected = await source.Content.ReadVerifiedAsync(tenant.ToString(), Assert.IsType<StandardResponseBody>(descriptor.Body).Content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True(firstDescriptorReads > 0, "The first HTTP response did not re-read its native frozen descriptor.");
        Assert.Equal(expected.ToArray(), await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(expected.ToArray(), await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, owner.Revision);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.PostAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnFrozenPersonalScopeBytesGivenExistingPostContractWithDerivedIdentity()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tracked = new TrackedContent(source.Content);
        await using var factory = Factory(source, tracked);
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "scope-native@example.test"), CultureInfo.InvariantCulture);
        var tenant = Uuid.CreateVersion4();
        var application = Uuid.CreateVersion4();
        var instance = Uuid.CreateVersion4();
        var registrant = Uuid.CreateVersion4();
        await source.Executor.ExecuteAsync(new DeclaredApplication(tenant, application), current =>
            AggregateOutcome.Commit(current.Declare("Payroll", "Run payroll", null, registrant, "Registrant", source.Clock.Now)),
            new RequestDispatchContext(RequestActor.System));
        await source.Executor.ExecuteAsync(new DeclaredSystemInstance(tenant, instance), current =>
            AggregateOutcome.Commit(current.Declare(application, "Production", "production", null, null,
                registrant, "Registrant", source.Clock.Now)), new RequestDispatchContext(RequestActor.System));
        var route = $"/api/v1/tenants/{tenant}/applications/{application}/system-instances/{instance}/access-review-scope-decisions";
        var body = new Dictionary<string, object>
        {
            ["expected_system_instance_revision"] = 1,
            ["expected_decision_count"] = 0,
            ["decision"] = "included",
            ["reason"] = "Production data",
            ["effective_from"] = DateTimeOffset.UtcNow,
        };

        // Act
        using var first = await client.PostAsJsonAsync(route, body);
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        var firstDescriptorReads = tracked.DescriptorReads;
        using var retry = await client.PostAsJsonAsync(route, body);
        var owner = await source.Reader.HydrateAsync(new SystemInstanceAccessReviewScope(tenant, instance));
        var fact = Assert.IsType<AccessReviewScopeDecided>(owner.Latest);
        var descriptor = await FirstResponses.ReplayAsync(source.Content, tenant.ToString(), fact.FirstResponse!);
        var expected = await source.Content.ReadVerifiedAsync(tenant.ToString(), Assert.IsType<StandardResponseBody>(descriptor.Body).Content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True(firstDescriptorReads > 0, "The first personal HTTP response did not re-read its native frozen descriptor.");
        Assert.Equal(expected.ToArray(), await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(expected.ToArray(), await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal(RbacIds.Member(tenant, user).ToString(), fact.Operation.Actor.Id);
        Assert.Equal(1, owner.Revision);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.PostAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(ControlSourceFixture source,
        TrackedContent tracked) => ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<IEventStore>();
        services.RemoveAll<IDomainEventReader>();
        services.RemoveAll<IImmutableContentStore>();
        services.RemoveAll<IPermissionAuthorizer>();
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        services.AddSingleton<IEventStore>(source.Events);
        services.AddSingleton<IDomainEventReader>(source.Events);
        services.AddSingleton<IImmutableContentStore>(tracked);
    }));

    sealed class TrackedContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int DescriptorReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema == FirstResponses.Schema)
                DescriptorReads++;
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
