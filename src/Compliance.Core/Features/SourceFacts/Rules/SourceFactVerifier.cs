using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.SourceFacts.Rules;

/// <summary>Shared fail-closed comparison between a stored record and a proof, used by every resolver.</summary>
static class SourceFactVerifier
{
    public static ResolvedSourceFact Build(EventStreamAddress stream, ulong resourcePosition, ulong realmPosition,
        ulong globalPosition, DomainEvent domainEvent)
    {
        var metadata = domainEvent.Metadata ?? throw new SourceFactVerificationException(
            "The source record carries no event metadata.");
        if (metadata.IsAudit)
            throw new SourceFactVerificationException("An audit record is not a source fact.");
        return new ResolvedSourceFact(domainEvent, new SourceFactLocator(stream, metadata.EventId, resourcePosition,
            realmPosition, globalPosition, metadata.AggregateVersion,
            ContentDigest.Sha256(domainEvent, domainEvent.GetType())));
    }

    public static VerifiedSourceProof<TEvent> Verify<TEvent>(SourceFactProof<TEvent> proof, ResolvedSourceFact? fact)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (fact is null)
            throw new SourceFactVerificationException("The proof names a record that does not exist.");
        var locator = fact.Locator;
        if (fact.Event is not TEvent typed || proof.EventKind != EventKind.Of(fact.Event))
            throw new SourceFactVerificationException("The stored event kind does not match the proof.");
        if (locator.Source.Realm != proof.Source.Realm)
            throw new SourceFactVerificationException("The stored record belongs to a different realm.");
        if (locator.Source != proof.Source)
            throw new SourceFactVerificationException("The stored record belongs to a different stream.");
        if (locator.EventId != proof.EventId)
            throw new SourceFactVerificationException("The stored event id does not match the proof.");
        if (locator.ResourcePosition != proof.PhysicalPosition || locator.RealmPosition != proof.RealmPosition ||
            locator.GlobalPosition != proof.GlobalPosition)
            throw new SourceFactVerificationException("The stored record positions do not match the proof.");
        if (locator.AggregateVersion != proof.OwnerRevision)
            throw new SourceFactVerificationException("The stored aggregate version does not match the proof.");
        if (!string.Equals(locator.PayloadSha256, proof.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new SourceFactVerificationException("The stored payload digest does not match the proof.");
        return new VerifiedSourceProof<TEvent>(locator.Source, locator.EventId, locator.ResourcePosition,
            locator.AggregateVersion, locator.PayloadSha256, typed, locator.GlobalPosition, locator.RealmPosition);
    }
}
