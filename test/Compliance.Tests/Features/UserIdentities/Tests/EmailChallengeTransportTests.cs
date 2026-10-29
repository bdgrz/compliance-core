using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

/// <summary>The email address routes: caller-chosen identities, native facts and exact HTTP retries.</summary>
[Trait("Category", "WebIntegration")]
public sealed class EmailChallengeTransportTests
{
    const string Email = "challenge-transport@example.com";

    [Fact]
    public async Task ShouldAcceptReserveIssueAndVerifyOnceGivenExactHttpRetries()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = Email });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await client.GetAsync("/auth/session");
        var userId = Uuid.Parse((await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        var path = $"/api/v1/users/{userId}/email-addresses/{Email}";
        var reserve = Uuid.CreateVersion4();
        var challenge = Uuid.CreateVersion4();

        // Act
        var reserved = await PutTwiceAsync($"{path}/{reserve}", new { expected_revision = -1 });
        var issued = await PutTwiceAsync($"{path}/challenges/{challenge}", new { });
        var issuedFact = (await FactsAsync(factory)).OfType<EmailChallengeIssued>().Single();
        var token = factory.Services.GetRequiredService<EmailChallengeTokenKeys>()
            .Derive(issuedFact.TokenKeyId, challenge, userId, Email, issuedFact.ExpiresAt);
        var verified = await PutTwiceAsync($"{path}/verifications/{challenge}", new { token });
        using var wrongToken = await client.PutAsJsonAsync($"{path}/verifications/{challenge}", new { token = new string('0', 64) });
        using var status = await client.GetAsync($"{path}/challenges/status");
        var facts = await FactsAsync(factory);

        // Assert
        Assert.All(new[] { reserved, issued, verified }, code => Assert.Equal(HttpStatusCode.OK, code));
        Assert.False(wrongToken.IsSuccessStatusCode);
        Assert.Equal("verified", (await status.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("delivery_status").GetString());
        Assert.Collection(facts,
            fact => Assert.Equal(reserve, Assert.IsType<EmailAddressReserved>(fact).Operation.LogicalId),
            fact => Assert.Equal(challenge, Assert.IsType<EmailChallengeIssued>(fact).Operation.LogicalId),
            fact => Assert.Equal(challenge, Assert.IsType<EmailAddressVerified>(fact).Operation.LogicalId));
        Assert.DoesNotContain(token, string.Join('\n', facts.Select(fact =>
            JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default.Options))), StringComparison.Ordinal);

        async Task<HttpStatusCode> PutTwiceAsync(string route, object body)
        {
            using var first = await client.PutAsJsonAsync(route, body);
            using var retry = await client.PutAsJsonAsync(route, body);
            Assert.Equal(first.StatusCode, retry.StatusCode);
            return first.StatusCode;
        }
    }

    static async Task<List<DomainEvent>> FactsAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                               .ReadAsync(new EmailAddress(Email).Stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }
}
