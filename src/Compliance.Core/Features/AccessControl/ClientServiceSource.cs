using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Verifies the actual native source tuple before a client preview reaction has effects.</summary>
static class ClientServiceSource
{
    public static async Task VerifyAsync(ISourceFactResolver sources, IImmutableContentStore content,
        EventStreamAddress stream, ulong offset, ClientIndependenceEvaluationAdmitted trigger, CancellationToken ct)
    {
        if (!Uuid.TryParse(stream.Realm, out var tenant) || stream != new ClientServiceIndependence(tenant).Stream ||
            trigger.Metadata is null || trigger.Metadata.AggregateId != new ClientServiceIndependence(tenant).Id ||
            trigger.Operation.Owner != stream)
            throw new SourceFactVerificationException("A preview source requires its exact native client-owner scope.");
        var (_, actual, _) = await SourceFactProofs.BuildAsync<ClientIndependenceEvaluationAdmitted>(sources, content,
            stream.Realm, NativeFactReference.Of(stream, trigger), offset, ct).ConfigureAwait(false);
        if (actual.Metadata != trigger.Metadata || ContentDigest.Sha256(actual, typeof(ClientIndependenceEvaluationAdmitted)) !=
            ContentDigest.Sha256(trigger, typeof(ClientIndependenceEvaluationAdmitted)))
            throw new SourceFactVerificationException("The client preview checkpoint differs from its committed payload and complete metadata.");
    }
}
