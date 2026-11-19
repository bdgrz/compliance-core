using Cntryl.Cassie.Connection;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Workforce.Projections;

namespace Bdgrz.Compliance.Hosting.Persistence;

static class CassieReadModelSchema
{
    internal static IReadOnlyList<string> Statements { get; } =
    [
        .. CassieWorkforceSchema.Statements,
        .. CassieMethodologyTemplateSchema.Statements,
        .. CassieRemediationWorkSchema.Statements,
    ];

    public static async Task InitializeAsync(CassieDataSource source, CancellationToken ct = default)
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
