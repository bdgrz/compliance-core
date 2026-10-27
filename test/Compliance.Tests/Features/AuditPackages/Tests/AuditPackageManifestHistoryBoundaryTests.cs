using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

public sealed class AuditPackageManifestHistoryBoundaryTests
{
    [Fact]
    public async Task ShouldRetainBoundedManifestAuthorityGivenManyActualNativeManifestVersions()
    {
        // Arrange
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var package = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var at = DateTimeOffset.UtcNow;
        var rules = new AuditPackageRules(tenant, program, package);
        var selection = new[]
        {
            new AuditPackageSelectionEntry("control_matrix", "control-source", "control", Uuid.CreateVersion4(),
                1, "csv", new string('b', 64), 512, "product_calculated"),
        };
        Assert.Null(rules.Create(new AuditPackageContent(Uuid.CreateVersion4(), "type_i", "type_i_baseline",
            Uuid.CreateVersion4(), new string('a', 64), "Exact package", selection), actor, "Author", at));
        Build(1);
        await PackageSeeds.WriteAsync(reader, writer, events, content, rules.Current!);
        for (var version = 2; version <= 32; version++)
        {
            Assert.Null(rules.Amend(rules.Revision, $"Exact package {version}", Uuid.CreateVersion4(),
                new string('a', 64), selection, actor, "Author", at.AddMinutes(version * 3)));
            Build(version);
            await PackageSeeds.AppendAsync(reader, writer, events, content, rules.Current!);
        }

        void Build(int version)
        {
            Assert.Null(rules.RequestBuild(rules.Revision, $"build-{version}", actor, "Author",
                at.AddMinutes(version * 3 + 1)));
            var current = rules.Current!.Versions[^1];
            var attempt = current.BuildAttempts[^1];
            Assert.Null(rules.RecordBuildResult(rules.Revision, attempt.BuildAttemptId,
                new AuditPackageBuildOutput(current.Manifest.AuditPackageManifestId, current.Manifest.ManifestSha256,
                    current.Manifest.Entries, Uuid.CreateVersion4(), new string('c', 64), 4096, null),
                null, "build-worker", at.AddMinutes(version * 3 + 2)));
        }

        // Act
        var owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, package));
        var retained = typeof(AuditPackageManifest).GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly).Select(field => field.GetValue(owner)).OfType<IEnumerable>()
            .Where(value => value is not string).Sum(value => value.Cast<object>().Count());

        // Assert
        Assert.Equal(32, owner.CurrentVersion);
        Assert.Equal(96, owner.Revision);
        Assert.True(retained <= 8, $"The command owner retained {retained} historical collection entries.");
    }
}
