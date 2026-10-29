using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

[Trait("Category", "WebIntegration")]
public sealed class NativeFirstResponseProductionHttpTests
{
    [Fact]
    public async Task ShouldReturnExactNativeUnitBytesGivenHttpRevisionRetryAfterLaterProgress()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        await using var factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IDomainEventReader>();
            services.RemoveAll<IImmutableContentStore>();
            services.AddSingleton<IEventStore>(source.Events);
            services.AddSingleton<IDomainEventReader>(source.Events);
            services.AddSingleton(source.Content);
        }));
        using var client = factory.CreateClient();
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "native-http@example.test"), CultureInfo.InvariantCulture);
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "HTTP-NATIVE");
        await source.SeedProgramAsync(tenant, program, user);
        var rules = new ControlRules(tenant, id);
        Assert.True(rules.Create(program, id, "HTTP-NATIVE", Content("First"), RbacIds.Member(tenant, user), "Author", source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        var command = Uuid.CreateVersion4();
        var route = $"/api/v1/tenants/{tenant}/programs/{program}/controls/{id}/draft/revisions/{command}";
        var body = Body(1, Content("Second"));

        // Act
        using var first = await client.PutAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var later = await client.PutAsJsonAsync($"/api/v1/tenants/{tenant}/programs/{program}/controls/{id}/draft/revisions/{Uuid.CreateVersion4()}", Body(2, Content("Third")));
        using var retry = await client.PutAsJsonAsync(route, body);
        var fact = Assert.IsType<ControlDraftRevised>((await source.FactsAsync(tenant, id))[1]);
        var frozen = await FirstResponses.ReplayAsync(source.Content, tenant.ToString(), fact.FirstResponse!);
        var bytes = await source.Content.ReadVerifiedAsync(tenant.ToString(), Assert.IsType<StandardResponseBody>(frozen.Body).Content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(frozen.HttpStatus, (int)first.StatusCode);
        Assert.Equal(bytes.ToArray(), await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(bytes.ToArray(), await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal("{}", await retry.Content.ReadAsStringAsync());
        Assert.Equal(frozen.ContentType, retry.Content.Headers.ContentType?.ToString());
        Assert.Equal(3, (await source.Controls.ReadAsync(tenant, program, id)).Revision);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.PutAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
    static Dictionary<string, object> Body(long revision, ControlDraftContent content) => new()
    {
        ["expected_revision"] = revision,
        ["content"] = JsonSerializer.SerializeToElement(content, ComplianceCoreJsonContext.Default.ControlDraftContent),
    };
    static ControlDraftContent Content(string title) => new(title, "Review access", "Management reviews access",
        "The security lead reviews access quarterly", ["Dated review record"]);
}
