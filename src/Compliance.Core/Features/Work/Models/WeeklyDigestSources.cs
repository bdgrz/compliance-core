using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Exact causal source at the finalized hydrated cut, with actual physical offsets and authenticated broker proof.</summary>
static class WeeklyDigestSources
{
    public static async Task<SourceFactProof<TEvent>> CaptureAsync<TEvent>(IDomainEventReader events,
        ISourceFactResolver sources, IImmutableContentStore content, WeeklyWorkDigest owner, Uuid eventId,
        CancellationToken ct) where TEvent : DomainEvent
    {
        if (!owner.ReplayCut.Allows(owner))
            throw new ImmutableContentUnavailableException("The exact weekly source cut is not finalized.");
        var history = await WeeklyWorkDigestHistory.ReadAsync(events, sources, owner, ct).ConfigureAwait(false);
        var expected = history.Facts.SingleOrDefault(fact => fact.Metadata.EventId == eventId);
        if (expected is not TEvent)
            throw new ImmutableContentIntegrityException("The weekly receiver does not name its exact source kind at this cut.");
        if (expected is WeeklyWorkDigestDeliveryRequested requested &&
            (requested.AttemptNumber != owner.SelectedAttemptNumber ||
             requested.DeliveryKey != WeeklyWorkDigest.DeliveryKeyFor(owner.Id, owner.SelectedAttemptNumber)))
            throw new ImmutableContentIntegrityException("The weekly callback does not answer its exact admitted attempt/key.");
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (record.ResourceOffset >= owner.CommittedStreamPosition)
                break;
            if (record.Event.Metadata.EventId != eventId)
                continue;
            if (record.Stream != owner.Stream || record.Event.Metadata != expected.Metadata ||
                ContentDigest.Sha256(record.Event, record.Event.GetType()) != ContentDigest.Sha256(expected, expected.GetType()))
                throw new ImmutableContentIntegrityException("The weekly source differs from the exact hydrated native payload and metadata.");
            var built = await SourceFactProofs.BuildAsync<TEvent>(sources, content, owner.TenantId.ToString(),
                NativeFactReference.Of(owner.Stream, expected), record.ResourceOffset, ct).ConfigureAwait(false);
            if (built.Fact.Metadata != expected.Metadata ||
                ContentDigest.Sha256(built.Fact, typeof(TEvent)) != ContentDigest.Sha256(expected, typeof(TEvent)))
                throw new ImmutableContentIntegrityException("The authenticated weekly source differs from the hydrated native source.");
            RequireSource(built.Fact, owner);
            return built.Proof;
        }
        throw new ImmutableContentUnavailableException("The exact weekly source position is not visible yet.");
    }
    public static void RequireSource(DomainEvent value, WeeklyWorkDigest owner)
    {
        switch (value)
        {
            case WeeklyWorkDigestScheduled scheduled:
                if (scheduled.TenantId != owner.TenantId || scheduled.DigestId != owner.Id || scheduled.MemberId != owner.MemberId ||
                    WeeklyWorkDigest.IdFor(scheduled.TenantId, scheduled.MemberId, scheduled.WeekStarting) != owner.Id ||
                    scheduled.ScheduledBy.Kind != "system_process" || scheduled.ScheduledBy.Id != WeeklyWorkDigest.SchedulerProcessId ||
                    scheduled.Metadata.Actor is not { Issuer: "bdgrz.system", Subject: WeeklyWorkDigest.SchedulerProcessId })
                    throw WeeklyDigestNative.Invalid("The weekly schedule is not its exact named scheduler/member/week source.");
                break;
            case WeeklyWorkDigestDeliveryRequested requested:
                if (requested.NoChange || requested.TenantId != owner.TenantId || requested.DigestId != owner.Id ||
                    requested.MemberId != owner.MemberId || requested.RequestedBy.Kind != "system_process" ||
                    requested.RequestedBy.Id != WeeklyWorkDigest.DeliveryProcessId ||
                    requested.Metadata.Actor is not { Issuer: "bdgrz.system", Subject: WeeklyWorkDigest.DeliveryProcessId })
                    throw WeeklyDigestNative.Invalid("Only an exact mutating named delivery admission can cause its result.");
                break;
            default:
                throw WeeklyDigestNative.Invalid("The weekly source has another causal kind.");
        }
    }
    public static async Task VerifyAsync<TEvent>(ISourceFactResolver sources, WeeklyWorkDigest owner,
        SourceFactProof<TEvent> proof, IReadOnlyList<DomainEvent> prefix, int expectedAttempt, CancellationToken ct) where TEvent : DomainEvent
    {
        var verified = await sources.VerifyAsync(proof, ct).ConfigureAwait(false);
        var expected = prefix.SingleOrDefault(value => value.Metadata.EventId == proof.EventId);
        if (expected is not TEvent || verified.Source != owner.Stream ||
            verified.Facts.Metadata != expected.Metadata ||
            ContentDigest.Sha256(verified.Facts, typeof(TEvent)) != ContentDigest.Sha256(expected, typeof(TEvent)))
            throw WeeklyDigestNative.Invalid("The weekly proof differs from its preceding hydrated source prefix.");
        RequireSource(verified.Facts, owner);
        if (verified.Facts is WeeklyWorkDigestDeliveryRequested requested &&
            (requested.AttemptNumber != expectedAttempt || requested.DeliveryKey != WeeklyWorkDigest.DeliveryKeyFor(owner.Id, expectedAttempt)))
            throw WeeklyDigestNative.Invalid("The weekly result proof belongs to another admitted attempt/key.");
    }
}
