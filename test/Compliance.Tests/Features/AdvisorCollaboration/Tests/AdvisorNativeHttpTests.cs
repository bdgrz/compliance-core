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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
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
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

[Trait("Category", "WebIntegration")]
public sealed class AdvisorNativeHttpTests
{
    [Fact]
    public async Task ShouldReplayOriginalUnitAndRequireCurrentPermissionGivenClosedHandoffHttpRetry()
    {
        // Arrange
        await using var source = new AdvisorHost();
        var (handoffId, _) = await source.SeedReadyAsync();
        var permissions = new CurrentPermission();
        await using var factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.RemoveAll<IImmutableContentStore>();
                services.AddSingleton(source.Services.GetRequiredService<IEventStore>());
                services.AddSingleton(source.Events);
                services.AddSingleton(source.Content);
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer>(permissions);
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                    new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
            }));
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "advisor-http@example.test"), CultureInfo.InvariantCulture);
        var program = new ProgramRules(source.TenantId, source.ProgramId);
        Assert.Null(program.Create("Advisor HTTP", new ProgramPlan(null, null, null, null, null, null),
            RbacIds.Member(source.TenantId, user), "Program author", source.Clock.GetUtcNow()));
        await ProgramSeeds.WriteAsync(source.Reader, source.Writer, source.Events, source.Content, program);
        var request = new CloseAdvisorHandoff(Uuid.CreateVersion4(), source.TenantId, source.ProgramId,
            handoffId, "The review is complete", 2);
        var route = $"/api/v1/tenants/{source.TenantId}/programs/{source.ProgramId}/advisor-handoffs/{handoffId}/closures/{request.CommandId}";
        Task<HttpResponseMessage> SendAsync(CloseAdvisorHandoff value) => client.PutAsync(route,
            new StringContent(JsonSerializer.Serialize(value, ComplianceCoreJsonContext.Default.CloseAdvisorHandoff), Encoding.UTF8, "application/json"));

        // Act
        using var first = await SendAsync(request);
        using var retry = await SendAsync(request);
        using var changed = await SendAsync(request with { Rationale = "Another authored intent" });
        permissions.Allowed = false;
        using var denied = await SendAsync(request);
        var stream = new AdvisorHandoff(source.TenantId, source.ProgramId, handoffId).Stream;
        var closed = Assert.Single(await source.EventsAsync<AdvisorHandoffClosed>(stream));
        var original = await FirstResponses.ReplayAsync(source.Content, source.TenantId.ToString(), closed.FirstResponse!);
        var bytes = await source.Content.ReadVerifiedAsync(source.TenantId.ToString(), Assert.IsType<StandardResponseBody>(original.Body).Content);

        // Assert
        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(bytes.ToArray(), await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(bytes.ToArray(), await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(3, (await source.Handoffs.ReadAsync(source.TenantId, source.ProgramId, handoffId)).Revision);
    }

    sealed class CurrentPermission : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}
