using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Verifies actual broker checkpoint tuple and bytes before staff reaction or projection effects.</summary>
static class FirmStaffSource
{
    public static async Task VerifyAsync<TEvent>(ISourceFactResolver sources, IImmutableContentStore content,
        EventStreamAddress stream, ulong offset, TEvent trigger, CancellationToken ct) where TEvent : DomainEvent, INativeOperationFact
    {
        if (!Uuid.TryParse(stream.Resource, null, out var id) || stream != new FirmStaffRecord(id).Stream ||
            trigger.Metadata is null || trigger.Metadata.AggregateId != new FirmStaffRecord(id).Id ||
            trigger.Operation.Owner != stream)
            throw new SourceFactVerificationException("A staff source requires its exact global staff-record identity.");
        var (_, actual, _) = await SourceFactProofs.BuildAsync<TEvent>(sources, content, FirmStaffContent.Realm,
            NativeFactReference.Of(stream, trigger), offset, ct).ConfigureAwait(false);
        if (actual.Metadata != trigger.Metadata || ContentDigest.Sha256(actual, typeof(TEvent)) !=
            ContentDigest.Sha256(trigger, typeof(TEvent)))
            throw new SourceFactVerificationException("The staff checkpoint differs from its actual committed payload and metadata.");
    }
}
