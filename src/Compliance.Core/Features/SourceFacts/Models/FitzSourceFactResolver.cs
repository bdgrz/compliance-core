using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Cntryl.Fitz;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.SourceFacts.Models;

/// <summary>
/// Resolves source facts through a global selector, the only READ scope that reports global offsets. The concrete
/// source route and resource position must both match before a record is deserialized.
/// </summary>
sealed class FitzSourceFactResolver(IStreamClient streams, IDomainEventSerializer serializer) : ISourceFactResolver
{
    public async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var record = await FindAsync(stream, resourcePosition, ct).ConfigureAwait(false);
        if (record.RealmOffset is not { } realmOffset || record.GlobalOffset is not { } globalOffset)
            throw new SourceFactVerificationException("The broker did not report realm and global offsets.");
        DomainEvent domainEvent;
        try
        {
            domainEvent = serializer.Deserialize(record.Body);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SourceFactVerificationException($"The source record cannot be deserialized: {exception.Message}");
        }
        return SourceFactVerifier.Build(stream, resourcePosition, realmOffset, globalOffset, domainEvent);
    }

    async Task<StreamRecord> FindAsync(EventStreamAddress stream, ulong resourcePosition, CancellationToken ct)
    {
        var concrete = stream.ToString();
        var selector = $"stream://*/{stream.Area}/{stream.Resource}";
        ulong next = 0;
        await foreach (var batch in streams.ReadAsync(selector, next, StreamReadMode.Replay,
                           new StreamReadOptions { BatchSize = 256 }, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            if (batch.FromOffset != next || batch.NextOffset < next)
                throw new SourceFactVerificationException("The source selector returned a non-contiguous replay cursor.");
            foreach (var item in batch.Items)
            {
                var candidate = item.Kind == StreamReadItemKind.Event ? item.Record : null;
                if (candidate is not null && candidate.Offset == resourcePosition &&
                    string.Equals(candidate.Route, concrete, StringComparison.Ordinal))
                    return candidate;
            }
            if (batch.CaughtUp)
                break;
            if (batch.NextOffset <= next)
                throw new SourceFactVerificationException("The source selector cannot make verified global progress.");
            next = batch.NextOffset;
        }
        throw new SourceFactUnavailableException($"No record exists at {concrete} offset {resourcePosition}.");
    }

    public async Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        var fact = await ResolveAsync(proof.Source, proof.PhysicalPosition, ct).ConfigureAwait(false);
        return SourceFactVerifier.Verify(proof, fact);
    }
}
