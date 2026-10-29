using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

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
