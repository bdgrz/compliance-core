using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Builds a <see cref="WebApplicationFactory{TEntryPoint}" /> that connects for real to the
///     e2e broker stack -- unlike <c>ComplianceWebTests.CreateBrokerFreeFactory</c>, nothing here
///     is stripped, so writes really go through Fitz KV and real JSON serialization, and scans
///     really go over the wire. That's the point: this is what makes the suite able to catch bugs
///     <c>Cntryl.Fitz.Testing.InMemoryKvClient</c>-backed unit tests structurally cannot.
/// </summary>
static class E2EAppFactory
{
    public static WebApplicationFactory<Program> Create(BrokerStackFixture broker,
        string? applicationName = null) => Create(broker.WebSocketEndpoint, applicationName);

    public static WebApplicationFactory<Program> Create(string webSocketEndpoint,
        string? applicationName = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", webSocketEndpoint);
            builder.UseSetting("Fitz:ApplicationName", applicationName ?? $"compliance-e2e-{Guid.NewGuid():N}");
            builder.UseSetting("Fitz:StartupTimeoutSeconds", "30");
            if (TestHostMode.Current is { } mode)
                builder.UseSetting("COMPLIANCE_HOST_MODE", mode);
        });
}
