using Cntryl.Cassie.Connection;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Workforce.Projections;

namespace Bdgrz.Compliance.Hosting.Persistence;

static class CassieReadModelSchema
{
    static readonly SemaphoreSlim SchemaSetup = new(1, 1);

    internal static IReadOnlyList<string> Statements { get; } =
    [
        .. CassieWorkforceSchema.Statements,
        .. CassieMethodologyTemplateSchema.Statements,
        .. CassieRemediationWorkSchema.Statements,
    ];

    public static Task InitializeAsync(CassieDataSource source, CancellationToken ct = default) =>
        WithSchemaSetupAsync(() => InitializeCoreAsync(source, ct), ct);

    internal static async Task<T> WithSchemaSetupAsync<T>(Func<Task<T>> setup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        await SchemaSetup.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await setup().ConfigureAwait(false);
        }
        finally
        {
            SchemaSetup.Release();
        }
    }

    internal static Task WithSchemaSetupAsync(Func<Task> setup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return WithSchemaSetupAsync(async () =>
        {
            await setup().ConfigureAwait(false);
            return true;
        }, ct);
    }

    internal static async Task InitializeCoreAsync(CassieDataSource source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        foreach (var statement in Statements)
        {
            command.CommandText = statement;
            _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
