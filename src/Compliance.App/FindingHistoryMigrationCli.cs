using System.Text.Json;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance;

public sealed record FindingHistoryMigrationCliOptions(string Action, Uuid TenantId,
    Uuid ProgramId, Uuid MigrationId);

public static class FindingHistoryMigrationCli
{
    const string CommandName = "finding-history-migration";
    static readonly string[] OptionNames = ["--tenant-id", "--program-id", "--migration-id"];
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0] == CommandName;

    public static bool TryParse(IReadOnlyList<string> args,
        out FindingHistoryMigrationCliOptions? options, out string? error)
    {
        options = null;
        error = null;
        if (!IsRequested(args))
            return false;
        if (args.Count < 2 || args[1] is not ("step" or "status"))
        {
            error = "Use finding-history-migration step|status with all three ID options.";
            return true;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 2; index < args.Count; index += 2)
        {
            var name = args[index];
            if (!OptionNames.Contains(name, StringComparer.Ordinal) ||
                index + 1 >= args.Count || values.ContainsKey(name))
            {
                error = $"Invalid or duplicate migration option: {name}.";
                return true;
            }
            values.Add(name, args[index + 1]);
        }

        if (values.Count != OptionNames.Length ||
            !Uuid.TryParse(values.GetValueOrDefault("--tenant-id"), null, out var tenantId) ||
            !Uuid.TryParse(values.GetValueOrDefault("--program-id"), null, out var programId) ||
            !Uuid.TryParse(values.GetValueOrDefault("--migration-id"), null, out var migrationId) ||
            tenantId == Uuid.Empty || programId == Uuid.Empty || migrationId == Uuid.Empty)
        {
            error = "Tenant, program, and migration IDs must each be a nonempty UUID.";
            return true;
        }

        options = new(args[1], tenantId, programId, migrationId);
        return true;
    }

    public static async Task<int> RunAsync(IReadOnlyList<string> args,
        CancellationToken ct = default)
    {
        if (!TryParse(args, out var options, out var error))
            return 2;
        if (options is null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        try
        {
            var environment = Host.CreateApplicationBuilder();
            var builder = CreateMigrationHostBuilder(environment.Configuration);
            using var host = builder.Build();
            await host.StartAsync(ct).ConfigureAwait(false);
            try
            {
                await using var scope = host.Services.CreateAsyncScope();
                var statusReader = scope.ServiceProvider
                    .GetRequiredService<FindingHistoryMigrationStatusReader>();
                if (options.Action == "status")
                {
                    var status = await statusReader.ReadAsync(options.TenantId, options.ProgramId,
                        options.MigrationId, ct).ConfigureAwait(false);
                    WriteStatus(status);
                    return status.IsStarted && status.ActiveMigrationId != options.MigrationId ? 1 : 0;
                }

                var before = await statusReader.ReadAsync(options.TenantId, options.ProgramId,
                    options.MigrationId, ct).ConfigureAwait(false);
                if (before.IsStarted && before.ActiveMigrationId != options.MigrationId)
                {
                    Console.Error.WriteLine("A different finding migration owns this program source stream.");
                    return 1;
                }
                if (!before.IsComplete)
                {
                    ulong? expectedOffset = before.IsStarted ? before.NextSourceOffset : null;
                    if (before.IsStarted && expectedOffset > before.FenceMarkerResourceOffset)
                    {
                        Console.Error.WriteLine("The durable finding migration cursor exceeds its source fence.");
                        return 1;
                    }

                    var actor = RequestActor.CreateSystem("migration:FindingHistoryV1", "bdgrz.system");
                    var metadata = new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null);
                    var dispatch = new RequestDispatchContext(actor, new DirectInvocation(), metadata);
                    var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
                        .DispatchAsync(new MigrateFindingHistory(options.TenantId, options.ProgramId,
                            options.MigrationId, expectedOffset), dispatch, ct).ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        Console.Error.WriteLine(result.Error?.Message ?? "The migration step failed.");
                        return 1;
                    }
                }

                var after = await statusReader.ReadAsync(options.TenantId, options.ProgramId,
                    options.MigrationId, ct).ConfigureAwait(false);
                WriteStatus(after);
                return after.IsStarted && after.ActiveMigrationId != options.MigrationId ? 1 : 0;
            }
            finally
            {
                await host.StopAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static HostApplicationBuilder CreateMigrationHostBuilder(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);
        _ = builder.Services.AddCompliance(builder.Configuration,
            developerAuthentication: false, requireRealEmailDelivery: false);
        var hostedServices = builder.Services.Where(static descriptor =>
            descriptor.ServiceType == typeof(IHostedService)).ToArray();
        var connection = hostedServices.Where(static descriptor =>
            descriptor.ImplementationFactory is not null).ToArray();
        if (connection.Length != 1)
            throw new InvalidOperationException(
                "The migration host could not identify its Fitz connection service.");
        foreach (var descriptor in hostedServices.Except(connection))
            _ = builder.Services.Remove(descriptor);
        return builder;
    }

    static void WriteStatus(FindingHistoryMigrationStatus status) =>
        Console.WriteLine(JsonSerializer.Serialize(status, JsonOptions));
}
