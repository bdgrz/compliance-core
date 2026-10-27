using Bdgrz.Compliance.Features.Risks.Aggregates;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Supplies explicit authored identities to the selected risk draft HTTP contracts in broker fixtures.</summary>
static class RiskDraftHttpRequests
{
    public static Uuid IdForCollection(string collection, string identifier)
    {
        var parts = collection.Split('/');
        return RiskDraft.IdFor(Uuid.Parse(parts[4], CultureInfo.InvariantCulture), Uuid.Parse(parts[6], CultureInfo.InvariantCulture), identifier);
    }
    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string collection, object body)
    {
        var request = JsonSerializer.SerializeToNode(body)!.AsObject();
        var risk = IdForCollection(collection, request["identifier"]!.GetValue<string>());
        request["risk_id"] = risk.ToString();
        return client.PutAsJsonAsync($"{collection}/{risk}", request);
    }
    public static Task<HttpResponseMessage> ReviseAsync(HttpClient client, string draft, object body)
    {
        var request = JsonSerializer.SerializeToNode(body)!.AsObject();
        request["command_id"] ??= Uuid.CreateVersion4().ToString();
        return client.PutAsJsonAsync(draft, request);
    }
}
