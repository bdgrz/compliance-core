using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Processes;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
/// Real persisted native domain fixtures with actual immutable content/source proofs. These seed business rule
/// histories; they do not claim recipient integration acceptance. Handler tests retain the real recipient gates.
/// </summary>
static class WeeklyDigestFacts
{
    public static async Task WriteAsync(IServiceProvider provider, Uuid tenant, Uuid digest, WeeklyWorkDigestRules rules)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var writer = services.GetRequiredService<IAggregateWriter>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var sources = services.GetRequiredService<ISourceFactResolver>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var current = await reader.HydrateAsync(new WeeklyWorkDigest(tenant, digest));
        foreach (var fact in rules.PendingEvents.Skip(checked((int)current.Version)))
        {
            if (fact is WeeklyWorkDigestScheduled scheduled)
            {
                Assert.True(current.Schedule(scheduled.MemberId, scheduled.WeekStarting, scheduled.ScheduleKey,
                    scheduled.ScheduledBy, scheduled.ScheduledAt).IsSuccess);
                await writer.SaveAsync(current, new RequestDispatchContext(RequestActor.CreateSystem(
                    WeeklyWorkDigest.SchedulerProcessId, "bdgrz.system")));
            }
            else
            {
                var command = Uuid.CreateVersion4();
                var attempt = fact switch
                {
                    WeeklyWorkDigestDeliveryRequested requested => requested.AttemptNumber,
                    WeeklyWorkDigestDeliveryRecorded recorded => recorded.AttemptNumber,
                    _ => (current.LatestAttempt?.Number ?? 0) + 1
                };
                var kind = fact is WeeklyWorkDigestDeliveryRecorded ? nameof(RecordWorkDigestDelivery) : nameof(DeliverWeeklyWorkDigest);
                current = await reader.HydrateAsync(new WeeklyWorkDigest(tenant, digest, attempt, command, kind));
                var history = await WeeklyWorkDigestHistory.ReadAsync(events, sources, current);
                SourceFactProof<WeeklyWorkDigestScheduled>? scheduleProof = null;
                SourceFactProof<WeeklyWorkDigestDeliveryRequested>? requestProof = null;
                DeliverWeeklyWorkDigest? delivery = null;
                RecordWorkDigestDelivery? recording = null;
                string? suppression = null;
                var actor = fact switch
                {
                    WeeklyWorkDigestDeliveryRequested requested => requested.RequestedBy,
                    WeeklyWorkDigestDeliveryRecorded recorded => recorded.RecordedBy,
                    WeeklyWorkDigestDeliverySuppressed suppressed => suppressed.SuppressedBy,
                    _ => throw new InvalidOperationException("Unsupported weekly fixture fact.")
                };
                var at = fact switch
                {
                    WeeklyWorkDigestDeliveryRequested requested => requested.RequestedAt,
                    WeeklyWorkDigestDeliveryRecorded recorded => recorded.AttemptedAt,
                    WeeklyWorkDigestDeliverySuppressed suppressed => suppressed.SuppressedAt,
                    _ => default
                };
                Uuid source;
                if (fact is WeeklyWorkDigestDeliveryRecorded result)
                {
                    var admitted = history.Facts.OfType<WeeklyWorkDigestDeliveryRequested>().Single(value =>
                        !value.NoChange && value.AttemptNumber == attempt);
                    source = admitted.Metadata.EventId;
                    requestProof = await WeeklyDigestSources.CaptureAsync<WeeklyWorkDigestDeliveryRequested>(events, sources, content,
                        current, source, CancellationToken.None);
                    recording = new RecordWorkDigestDelivery(command, tenant, digest, source, current.Revision, attempt,
                        result.DeliveryKey, result.Outcome, result.AttemptedAt, result.FailureCode);
                }
                else
                {
                    source = Assert.Single(history.Facts.OfType<WeeklyWorkDigestScheduled>()).Metadata.EventId;
                    scheduleProof = await WeeklyDigestSources.CaptureAsync<WeeklyWorkDigestScheduled>(events, sources, content,
                        current, source, CancellationToken.None);
                    delivery = new DeliverWeeklyWorkDigest(command, tenant, current.MemberId, digest, source,
                        current.Revision, attempt, WeeklyWorkDigest.DeliveryKeyFor(digest, attempt));
                    suppression = (fact as WeeklyWorkDigestDeliverySuppressed)?.Reason;
                }
                var attribution = new ActorAttribution(WeeklyWorkDigest.DeliveryProcessId, "bdgrz.system");
                var mutation = new WeeklyDigestMutationContent(kind, delivery, recording, actor, attribution, at,
                    current.Header!, current.SelectedAttempt, current.LatestAttempt, suppression, scheduleProof, requestProof, false);
                var prepared = current.Prepare(mutation);
                Assert.True(prepared.IsSuccess, prepared.Error?.Message);
                var value = prepared.Value!;
                var operation = new OperationProvenance(command, command, source, kind, actor, current.Stream,
                    RootOperation.InitialEpisode, (ulong)current.Revision, WeeklyDigestNative.Intent(value.Change), at,
                    current.Stream, current.Stream, current.Stream);
                var change = await NativeContent.WriteAsync(content, tenant, WeeklyDigestNative.ChangeSchema, value.Change);
                var frozen = await NativeContent.FreezeUnitAsync(content, tenant);
                var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), frozen.Response);
                var applied = current.Append(value, operation, change, ((StandardResponseBody)frozen.Response.Body).Content,
                    first, frozen.Result);
                Assert.True(applied.IsSuccess, applied.Error?.Message);
                await writer.SaveAsync(current, new RequestDispatchContext(RequestActor.CreateSystem(
                    WeeklyWorkDigest.DeliveryProcessId, "bdgrz.system"), new DirectInvocation(),
                    new RequestMetadata(command, command, source)));
            }
            current = await reader.HydrateAsync(new WeeklyWorkDigest(tenant, digest));
        }
    }
    public static ValueTask<Result> ReplayAsync(IServiceProvider services, DeliverWeeklyWorkDigest request) =>
        Operations(services).DeliverAsync(new Cntryl.Portia.RequestContext<DeliverWeeklyWorkDigest>(request,
            Context(request.CommandId, request.ScheduleEventId)), CancellationToken.None);
    public static ValueTask<Result> ReplayAsync(IServiceProvider services, RecordWorkDigestDelivery request) =>
        Operations(services).RecordAsync(new Cntryl.Portia.RequestContext<RecordWorkDigestDelivery>(request,
            Context(request.CommandId, request.DeliveryRequestEventId)), CancellationToken.None);
    static RequestDispatchContext Context(Uuid command, Uuid source) => new(RequestActor.CreateSystem(
        WeeklyWorkDigest.DeliveryProcessId, "bdgrz.system"), new DirectInvocation(), new RequestMetadata(command, command, source));
    static WeeklyDigestOperations Operations(IServiceProvider services) => new(services.GetRequiredService<NativeOperationExecutor>(),
        services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<ISourceFactResolver>(),
        services.GetRequiredService<IImmutableContentStore>(), new WeeklyWorkDigestRecipientResolver(
            services.GetRequiredService<IAggregateReader>(), new NoAddresses()), new FrozenClock());
    sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
    }
    sealed class NoAddresses : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) => ValueTask.FromResult<EmailAddressView?>(null);
        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<EmailAddressView>([], null));
    }
}
