using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

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
