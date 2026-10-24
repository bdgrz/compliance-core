using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the complete service history of a client service owner from its retained immutable content.</summary>
static class ClientServiceHistory
{
    /// <summary>Every recorded service of the owner, in record order, as the complete original views.</summary>
    public static async Task<IReadOnlyList<NonattestServiceView>> ReadAsync(IImmutableContentStore content,
        ClientServiceIndependence owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(owner);
        var realm = IndependenceContent.Realm(owner.TenantId);
        var services = new List<NonattestServiceView>(owner.Services.Count);
        foreach (var recorded in owner.Services)
            services.Add(await content.ReadJsonAsync<NonattestServiceView>(realm, recorded.ServiceContent, ct)
                .ConfigureAwait(false));
        return Array.AsReadOnly(services.ToArray());
    }
}
