using Bdgrz.Compliance.Features.Providers.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

[Trait("Category", "WebIntegration")]
public sealed class ProviderAssuranceTransportTests
{
    [Fact]
    public async Task ShouldRecordReviseAndReadCoverageGivenAuthorizedHttpCaller()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "assurance-http@example.com");
        var tenantId = Uuid.CreateVersion4();
        var otherTenant = Uuid.CreateVersion4();
        var providerId = await RecordProviderAsync(factory.Services, tenantId);
        var otherProviderId = await RecordProviderAsync(factory.Services, otherTenant);
        var reportId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();

        // Act
        using var recorded = await client.PutAsync($"{Path(tenantId, providerId)}/assurance-reports/{reportId}", Json("content", Report()));
        using var recordedRetry = await client.PutAsync($"{Path(tenantId, providerId)}/assurance-reports/{reportId}", Json("content", Report()));
        using var changed = await client.PutAsync($"{Path(tenantId, providerId)}/assurance-reports/{reportId}",
            Json("content", Report() with { Issuer = "Other LLP" }));
        var revisionId = Uuid.CreateVersion4();
        var revisionBody = new StringContent("{\"report_id\":\"" + reportId + "\",\"expected_revision\":1,\"content\":" +
            JsonSerializer.Serialize(Report() with { Issuer = "Renamed LLP" }, ComplianceCoreJsonContext.Default.AssuranceReportContent) + "}",
            Encoding.UTF8, "application/json");
        using var revised = await client.PutAsync($"{Path(tenantId, providerId)}/assurance-reports/revise-assurance-report/{revisionId}", revisionBody);
        using var review = await client.PutAsync($"{Path(tenantId, providerId)}/reviews/{reviewId}", Json("content", Review(reportId)));
        using var reviewRetry = await client.PutAsync($"{Path(tenantId, providerId)}/reviews/{reviewId}", Json("content", Review(reportId)));
        using var invalid = await client.PutAsync($"{Path(tenantId, providerId)}/reviews/{Uuid.CreateVersion4()}",
            Json("content", Review(reportId) with { Conclusion = "approved" }));
        await AssuranceProjectionCatchUp.RunAsync(factory.Services, tenantId);
        using var reports = await client.GetAsync($"{Path(tenantId, providerId)}/assurance-reports?limit=10");
        using var coverage = await client.GetAsync($"{Path(tenantId, providerId)}/assurance-coverage?as_of=2026-12-31");
        using var foreignProvider = await client.GetAsync($"{Path(otherTenant, providerId)}/assurance-reports");
        using var foreignCoverage = await client.GetAsync($"{Path(tenantId, otherProviderId)}/assurance-coverage");
        using var foreignRecord = await client.PutAsync($"{Path(otherTenant, providerId)}/assurance-reports/{Uuid.CreateVersion4()}", Json("content", Report()));
        using var otherReports = await client.GetAsync($"{Path(otherTenant, otherProviderId)}/assurance-reports");

        // Assert
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(await recorded.Content.ReadAsStringAsync(), await recordedRetry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, review.StatusCode);
        using var accepted = JsonDocument.Parse(await review.Content.ReadAsStringAsync());
        Assert.Equal(reviewId.ToString(), accepted.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/tenants/{tenantId}/operations/{reviewId}", accepted.RootElement.GetProperty("status_url").GetString());
        Assert.True(review.Headers.Contains("Location"));
        Assert.Equal(HttpStatusCode.Accepted, reviewRetry.StatusCode);
        Assert.Equal(await review.Content.ReadAsStringAsync(), await reviewRetry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reports.StatusCode);
        using var reportPage = JsonDocument.Parse(await reports.Content.ReadAsStringAsync());
        var item = Assert.Single(reportPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, item.GetProperty("revision").GetInt64());
        Assert.Equal("soc2_type2", item.GetProperty("content").GetProperty("report_kind").GetString());
        Assert.Equal("Renamed LLP", item.GetProperty("content").GetProperty("issuer").GetString());
        Assert.Equal("2026-06-30", item.GetProperty("content").GetProperty("period_end").GetString());
        Assert.Equal(0, item.GetProperty("exception_count").GetInt32());
        using var view = JsonDocument.Parse(await coverage.Content.ReadAsStringAsync());
        Assert.Equal("no_review", view.RootElement.GetProperty("status").GetString());
        Assert.Equal("2026-12-31", view.RootElement.GetProperty("as_of").GetString());
        Assert.Equal("uncovered_after_period", Assert.Single(view.RootElement.GetProperty("reports").EnumerateArray())
            .GetProperty("period_coverage").GetString());
        Assert.Equal(HttpStatusCode.NotFound, foreignProvider.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCoverage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRecord.StatusCode);
        using var otherPage = JsonDocument.Parse(await otherReports.Content.ReadAsStringAsync());
        Assert.Equal(0, otherPage.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ShouldRecordCloseAndLinkGapGivenAuthorizedHttpCaller()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "assurance-gap@example.com");
        var tenantId = Uuid.CreateVersion4();
        var providerId = await RecordProviderAsync(factory.Services, tenantId);
        var gapId = Uuid.CreateVersion4();

        // Act
        using var link = await client.PutAsync(
            $"{Path(tenantId, providerId)}/coverage-gaps/{gapId}/risk-acceptances/link-provider-coverage-gap-risk-acceptance/{Uuid.CreateVersion4()}",
            new StringContent("{\"program_id\":\"" + Uuid.CreateVersion4() + "\",\"expected_gap_revision\":1,\"risk_id\":\"" +
                Uuid.CreateVersion4() + "\",\"acceptance_id\":\"" + Uuid.CreateVersion4() + "\"}", Encoding.UTF8, "application/json"));
        using var close = await client.PutAsync(
            $"{Path(tenantId, providerId)}/coverage-gaps/close-provider-coverage-gap/{Uuid.CreateVersion4()}",
            new StringContent("{\"gap_id\":\"" + gapId + "\",\"expected_revision\":1,\"content\":" +
                JsonSerializer.Serialize(new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report",
                    Uuid.CreateVersion4(), 2, "Covered."), ComplianceCoreJsonContext.Default.ProviderCoverageGapClosureContent) + "}",
                Encoding.UTF8, "application/json"));
        using var missing = await client.GetAsync($"{Path(tenantId, providerId)}/coverage-gaps/{gapId}");

        // Assert: both routes bind their command id and reach the gap owner, which does not know the gap.
        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, close.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ShouldRecordReportAndReadCoverageGivenAuthorizedMcpCaller()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "assurance-mcp@example.com");
        var tenantId = Uuid.CreateVersion4();
        var providerId = await RecordProviderAsync(factory.Services, tenantId);
        var reportId = Uuid.CreateVersion4();
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));

        // Act
        var recorded = await scenario.When("bdgrz.provider.assurance_report.record", new Dictionary<string, object?>
        {
            ["report_id"] = reportId.ToString(),
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
            ["content"] = JsonSerializer.SerializeToElement(Report(), ComplianceCoreJsonContext.Default.AssuranceReportContent),
        }).ExpectSuccess();
        var revised = await scenario.When("bdgrz.provider.assurance_report.revise", new Dictionary<string, object?>
        {
            ["command_id"] = Uuid.CreateVersion4().ToString(),
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
            ["report_id"] = reportId.ToString(),
            ["expected_revision"] = 1,
            ["content"] = JsonSerializer.SerializeToElement(Report() with { Issuer = "Renamed LLP" }, ComplianceCoreJsonContext.Default.AssuranceReportContent),
        }).ExpectSuccess();
        await AssuranceProjectionCatchUp.RunAsync(factory.Services, tenantId);
        var reports = await scenario.When("bdgrz.provider.assurance_reports.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
        }).ExpectSuccess();
        var coverage = await scenario.When("bdgrz.provider.assurance_coverage.get", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
            ["as_of"] = "2026-06-30",
        }).ExpectSuccess();

        // Assert
        Assert.Equal(1, Result(recorded).GetProperty("revision").GetInt64());
        Assert.Equal(2, Result(revised).GetProperty("revision").GetInt64());
        var item = Assert.Single(Result(reports).GetProperty("items").EnumerateArray());
        Assert.Equal("Renamed LLP", item.GetProperty("content").GetProperty("issuer").GetString());
        Assert.Equal("no_review", Result(coverage).GetProperty("status").GetString());
        Assert.Equal("covers_as_of", Assert.Single(Result(coverage).GetProperty("reports").EnumerateArray()).GetProperty("period_coverage").GetString());
    }

    static JsonElement Result(McpCallSnapshot snapshot) => Assert.IsType<JsonElement>(snapshot.StructuredJson).GetProperty("result");

    static string Path(Uuid tenant, Uuid provider) => $"/api/v1/tenants/{tenant}/providers/{provider}";

    static async Task LoginAsync(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = email });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    static StringContent Json(string name, AssuranceReportContent content) => new("{\"" + name + "\":" +
        JsonSerializer.Serialize(content, ComplianceCoreJsonContext.Default.AssuranceReportContent) + "}", Encoding.UTF8, "application/json");

    static StringContent Json(string name, ProviderReviewContent content) => new("{\"" + name + "\":" +
        JsonSerializer.Serialize(content, ComplianceCoreJsonContext.Default.ProviderReviewContent) + "}", Encoding.UTF8, "application/json");

    static async Task<Uuid> RecordProviderAsync(IServiceProvider services, Uuid tenantId)
    {
        var providerId = Uuid.CreateVersion4();
        await NamedRecordFacts.SeedProviderAsync(services, tenantId, providerId, MaterialProvider());
        return providerId;
    }
}
