using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>A captured read-side inventory of one program's evidence requests; this is never retained as command state.</summary>
public sealed class EvidenceRequestSnapshot(IReadOnlyList<EvidenceRequestView> requests)
{
    /// <summary>Every request of the program in the order opened.</summary>
    public IReadOnlyList<EvidenceRequestView> Requests { get; } = requests;

    /// <summary>The request, or null when the program has no such request.</summary>
    public EvidenceRequestView? Find(Uuid requestId) =>
        Requests.FirstOrDefault(request => request.EvidenceRequestId == requestId);

    /// <summary>Every request in the order opened.</summary>
    public IReadOnlyList<EvidenceRequestView> ReadAll() => Requests;

    /// <summary>The requests that are fulfilled right now.</summary>
    public IReadOnlySet<Uuid> FulfilledIds() => Requests
        .Where(static request => request.Status == EvidenceRequest.Fulfilled)
        .Select(static request => request.EvidenceRequestId).ToHashSet();
}
