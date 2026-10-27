using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Hosting;

[Trait("Category", "WebIntegration")]
public sealed class TenantSlugLoggingTests
{
    const string Slug = "confidential-client-name";

    public static TheoryData<string, string, string> Overrides => new()
    {
        { "Logging:LogLevel", "Microsoft.AspNetCore", "Trace" },
        { "Logging:Capture:LogLevel", "Microsoft.AspNetCore", "Trace" },
        { "Logging:LogLevel", "microsoft.aspnetcore.routing", "Debug" },
        { "Logging:LogLevel", "Microsoft.AspNetCore*", "Trace" },
        { "Logging:Capture:LogLevel", "Microsoft.AspNetCore.Hosting.Diagnostic*", "Critical" },
    };

    [Theory]
    [MemberData(nameof(Overrides))]
    public async Task ShouldKeepSlugOutOfLogsGivenVerboseLoggingConfiguration(string levels,
        string category, string level)
    {
        // Arrange
        var capture = new CapturingLoggerProvider();
        await using var factory = CreateFactory(capture, levels, category, level);
        using var client = factory.CreateClient();

        // Act
        // Web routes belong to the separate client. If they reach this backend, the slug remains
        // in an unmatched request path and must still stay out of framework logs.
        using var browser = await client.GetAsync($"/{Slug}/controls", CancellationToken.None);
        using var root = await client.GetAsync($"/{Slug}", CancellationToken.None);
        // Unauthenticated: this proves the API request pipeline, not the resolution handler.
        using var resolution = await client.GetAsync($"/api/v1/tenant-slugs/{Slug}/mine",
            CancellationToken.None);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, browser.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, root.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resolution.StatusCode);
        Assert.NotEmpty(capture.Entries);
        var leaks = capture.Entries
            .Where(entry => entry.Contains(Slug, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.True(leaks.Length == 0,
            $"Organization slug appeared in logs:{Environment.NewLine}{string.Join(Environment.NewLine, leaks)}");
    }

    [Theory]
    [MemberData(nameof(Overrides))]
    public async Task ShouldDisableRequestPathLoggingGivenOperatorOverride(string levels,
        string category, string level)
    {
        // Arrange
        var capture = new CapturingLoggerProvider();
        await using var factory = CreateFactory(capture, levels, category, level);
        using var client = factory.CreateClient();

        // Act
        var loggers = factory.Services.GetRequiredService<ILoggerFactory>();

        // Assert
        Assert.False(loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics")
            .IsEnabled(LogLevel.Critical));
        Assert.False(loggers.CreateLogger("Microsoft.AspNetCore.Routing.Matching.DfaMatcher")
            .IsEnabled(LogLevel.Information));
        Assert.False(loggers.CreateLogger("Microsoft.AspNetCore.StaticAssets")
            .IsEnabled(LogLevel.Information));
        Assert.True(loggers.CreateLogger("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware")
            .IsEnabled(LogLevel.Error));
        Assert.True(loggers.CreateLogger("Bdgrz.Compliance").IsEnabled(LogLevel.Trace));
    }

    static WebApplicationFactory<Program> CreateFactory(CapturingLoggerProvider capture,
        string levels, string category, string level) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            ProductionEmailDeliveryTestConfiguration.Apply(builder);
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", "bdgrz-test-session-signing-key-000001");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-slug-logging-tests");
            // An operator raising framework verbosity, globally or for one provider, must not
            // re-enable request-path logging.
            builder.UseSetting($"{levels}:Default", "Trace");
            builder.UseSetting($"{levels}:{category}", level);
            builder.ConfigureLogging(logging => logging.AddProvider(capture));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(item =>
                             item.ServiceType == typeof(IHostedService) &&
                             item.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                    services.Remove(descriptor);
            });
        });

    [ProviderAlias("Capture")]
    sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public void Dispose()
        {
        }

        sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                provider._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var entry = new System.Text.StringBuilder()
                    .Append(category).Append(' ').Append(formatter(state, exception));
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    foreach (var value in values)
                        entry.Append(' ').Append(value.Key).Append('=').Append(value.Value);
                }

                provider._scopes.ForEachScope((scope, builder) =>
                {
                    builder.Append(" scope=").Append(scope);
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                    {
                        foreach (var value in scopeValues)
                            builder.Append(' ').Append(value.Key).Append('=').Append(value.Value);
                    }
                }, entry);
                if (exception is not null)
                    entry.Append(' ').Append(exception);
                provider.Entries.Enqueue(entry.ToString());
            }
        }
    }
}
