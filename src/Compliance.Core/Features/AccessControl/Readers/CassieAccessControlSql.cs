using System.Globalization;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

static class CassieAccessControlSql
{
    public static void Bind(CassieCommand command, params (string Name, object Value)[] parameters)
    {
        foreach (var parameter in parameters)
            _ = command.Parameters.Add(new CassieParameter { ParameterName = parameter.Name, Value = parameter.Value });
    }

    public static void RequireScope(CheckpointIdentity identity, Uuid tenantId)
    {
        if (identity.Pattern != EventStreamPattern.ForPattern(tenantId.ToString()))
            throw new InvalidOperationException("An access directory fact must belong to its bound tenant projection batch.");
    }

    public static string RowKey(string projection, params string[] identity) =>
        string.Concat(new[] { projection }.Concat(identity).Select(static part =>
            part.Length.ToString(CultureInfo.InvariantCulture) + ":" + part));

    public static Uuid Id(string value) => Uuid.Parse(value, CultureInfo.InvariantCulture);
}
