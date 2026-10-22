using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingHistoryMigrationCliTests
{
    [Fact]
    public void ShouldParseExplicitStepAndIdsGivenMigrationCommand()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        string[] args = ["finding-history-migration", "step", "--tenant-id", tenantId.ToString(),
            "--program-id", programId.ToString(), "--migration-id", migrationId.ToString()];

        // Act
        var requested = FindingHistoryMigrationCli.TryParse(args, out var options, out var error);

        // Assert
        Assert.True(requested);
        Assert.Null(error);
        Assert.Equal(new FindingHistoryMigrationCliOptions("step", tenantId, programId, migrationId),
            options);
    }

    [Theory]
    [InlineData("step")]
    [InlineData("status")]
    public void ShouldRejectMissingOrRepeatedIdsGivenMigrationCommand(string action)
    {
        // Arrange
        string[] args = ["finding-history-migration", action, "--tenant-id",
            Uuid.CreateVersion4().ToString(), "--tenant-id", Uuid.CreateVersion4().ToString()];

        // Act
        var requested = FindingHistoryMigrationCli.TryParse(args, out var options, out var error);

        // Assert
        Assert.True(requested);
        Assert.Null(options);
        Assert.NotNull(error);
    }

    [Fact]
    public void ShouldLeaveOtherApplicationArgumentsUnclaimedGivenNonMigrationCommand()
    {
        // Arrange
        string[] args = ["--urls", "http://127.0.0.1:8080"];

        // Act
        var requested = FindingHistoryMigrationCli.TryParse(args, out var options, out var error);

        // Assert
        Assert.False(requested);
        Assert.Null(options);
        Assert.Null(error);
    }

    [Fact]
    public void ShouldStartOnlyFitzConnectionGivenMigrationHostComposition()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance-migration",
            })
            .Build();

        // Act
        var builder = FindingHistoryMigrationCli.CreateMigrationHostBuilder(configuration);
        var hostedServices = builder.Services.Where(static descriptor =>
            descriptor.ServiceType == typeof(IHostedService)).ToArray();

        // Assert
        var connection = Assert.Single(hostedServices);
        Assert.NotNull(connection.ImplementationFactory);
    }
}
