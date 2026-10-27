using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Supplies explicit authored identities to the selected Commitment HTTP contracts used by broker acceptance cases.</summary>
static class CommitmentHttpRequests
{
    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string collection, object body)
    {
        var parts = collection.Split('/');
        var tenant = Uuid.Parse(parts[4], CultureInfo.InvariantCulture);
        var program = Uuid.Parse(parts[6], CultureInfo.InvariantCulture);
        var request = JsonSerializer.SerializeToNode(body)!.AsObject();
        var draft = CommitmentDraft.IdFor(tenant, program, request["kind"]!.GetValue<string>(), request["identifier"]!.GetValue<string>());
        request["draft_id"] = draft.ToString();
        return client.PutAsJsonAsync($"{collection}/{draft}", request);
    }

    public static Task<HttpResponseMessage> ReviseAsync(HttpClient client, string draft, object body)
    {
        var request = WithCommand(body, out var command);
        return client.PutAsJsonAsync($"{draft}/draft-revisions/{command}", request);
    }

    public static Task<HttpResponseMessage> DecideAsync(HttpClient client, string decisionCollection, object body)
    {
        var request = WithCommand(body, out var command);
        return client.PutAsJsonAsync($"{decisionCollection}/{command}", request);
    }

    static JsonObject WithCommand(object body, out Uuid command)
    {
        var request = JsonSerializer.SerializeToNode(body)!.AsObject();
        command = request["command_id"] is { } retained
            ? Uuid.Parse(retained.GetValue<string>(), CultureInfo.InvariantCulture) : Uuid.CreateVersion4();
        request["command_id"] = command.ToString();
        return request;
    }
}
