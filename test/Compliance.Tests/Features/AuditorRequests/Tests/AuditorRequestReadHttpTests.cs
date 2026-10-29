using Bdgrz.Compliance.Tests.Testing;
using System.Net;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

[Trait("Category", "WebIntegration")]
public sealed class AuditorRequestReadHttpTests
{
    [Fact]
    public async Task ShouldReadExactAuditorRequestGivenScopedAuthorizedHttp()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var program = new ProgramRules(fixture.TenantId, fixture.ProgramId);
        Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            fixture.MemberId, "Program author", AuditorRequestFixture.Now));
        await ProgramSeeds.WriteAsync(fixture.Reader, fixture.Writer, fixture.Events, fixture.Content, program);
        await using var factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.RemoveAll<IImmutableContentStore>();
                services.AddSingleton<IEventStore>(fixture.Events);
                services.AddSingleton<IDomainEventReader>(fixture.Events);
                services.AddSingleton(fixture.Content);
            }));
        using var client = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(client, "auditor-request-reader@example.test");
        var route = $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/auditor-requests/{requestId}";

        // Act
        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var view = JsonSerializer.Deserialize(body, ComplianceCoreJsonContext.Default.AuditorRequestView);
        Assert.NotNull(view);
        Assert.Equal(requestId, view.AuditorRequestId);
        Assert.Equal(fixture.TenantId, view.TenantId);
        Assert.Equal(fixture.ProgramId, view.ProgramId);
        Assert.Equal("External request wording", view.Wording);
        Assert.Single(view.TermsHistory);
    }
}
