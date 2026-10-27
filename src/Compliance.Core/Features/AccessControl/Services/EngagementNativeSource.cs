using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Authenticates an engagement reactor checkpoint before any durable effect.</summary>
static class EngagementNativeSource
{
    public static async Task VerifyAsync<T>(ISourceFactResolver sources, IImmutableContentStore content,
        EventStreamAddress stream, ulong offset, T trigger, CancellationToken ct) where T : DomainEvent, INativeOperationFact
    {
        if (!Uuid.TryParse(stream.Realm, out var tenant) || !Uuid.TryParse(stream.Resource, out var engagement) ||
            stream != new ServiceEngagementIndependence(tenant, engagement).Stream || trigger.Metadata is null ||
            trigger.Metadata.AggregateId != new ServiceEngagementIndependence(tenant, engagement).Id ||
            trigger.Operation.Owner != stream)
            throw new SourceFactVerificationException("An engagement checkpoint requires its exact native scope.");
        var (_, actual, _) = await SourceFactProofs.BuildAsync<T>(sources, content, stream.Realm,
            NativeFactReference.Of(stream, trigger), offset, ct).ConfigureAwait(false);
        if (actual.Metadata != trigger.Metadata || ContentDigest.Sha256(actual, typeof(T)) != ContentDigest.Sha256(trigger, typeof(T)))
            throw new SourceFactVerificationException("The engagement checkpoint differs from its committed payload or complete metadata.");
    }
}
