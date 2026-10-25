using Cntryl.Fitz;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Seals a per-anchor global frontier from the verified broker tuple at its captured physical tail.</summary>
static class IndependenceSourceCut
{
    public static async Task<AuthoritativeReadCut> CaptureAsync(IDomainEventReader events, ISourceFactResolver sources,
        Aggregate owner, TimeProvider clock, CancellationToken ct)
    {
        try
        {
            return await CaptureCoreAsync(events, sources, owner, clock, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException && !ct.IsCancellationRequested && Retryability.IsRetryable(error))
        {
            throw new ImmutableContentUnavailableException($"The captured source frontier is temporarily unavailable: {error.Message}");
        }
    }

    static async Task<AuthoritativeReadCut> CaptureCoreAsync(IDomainEventReader events, ISourceFactResolver sources,
        Aggregate owner, TimeProvider clock, CancellationToken ct)
    {
        var anchor = $"{owner.Stream.Realm}/{owner.Stream.Area}/{owner.Stream.Resource}";
        ct.ThrowIfCancellationRequested();
        if (owner.CommittedStreamPosition == 0)
        {
            if (owner.Version != 0)
                throw new ImmutableContentIntegrityException("A nonempty native cut has no committed physical frontier.");
            return new AuthoritativeReadCut(anchor, 0, clock.GetUtcNow());
        }
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var tailPosition = owner.CommittedStreamPosition - 1;
        DomainEventRecord? tail = null;
        await foreach (var record in events.ReadAsync(owner.Stream, tailPosition, ct).ConfigureAwait(false))
        {
            tail = record;
            break;
        }
        if (tail is null)
            throw new ImmutableContentUnavailableException("The exact captured source physical tail is not visible yet.");
        if (tail.Stream != owner.Stream || tail.ResourceOffset != tailPosition || tail.Event.Metadata is null)
            throw new ImmutableContentIntegrityException("The source cut tail has another exact stream or physical position.");
        if (tail.Event.Metadata.IsAudit)
            throw new ImmutableContentUnavailableException("An audit-ending cut cannot be proven by the installed non-audit source resolver.");
        if (facts.Count == 0 || facts[^1].GetType() != tail.Event.GetType() || !facts[^1].Equals(tail.Event) ||
            facts[^1].Metadata != tail.Event.Metadata || tail.Event.Metadata.AggregateVersion != owner.Version)
            throw new ImmutableContentIntegrityException("The source cut tail differs from its exact captured native prefix.");
        try
        {
            var resolved = await sources.ResolveAsync(owner.Stream, tailPosition, ct).ConfigureAwait(false);
            var locator = resolved.Locator;
            if (locator.Source != owner.Stream || locator.ResourcePosition != tailPosition ||
                locator.EventId != tail.Event.Metadata.EventId || locator.AggregateVersion != owner.Version ||
                resolved.Event.GetType() != tail.Event.GetType() || !resolved.Event.Equals(tail.Event) ||
                resolved.Event.Metadata != tail.Event.Metadata || locator.PayloadSha256 != ContentDigest.Sha256(tail.Event, tail.Event.GetType()) ||
                locator.GlobalPosition == ulong.MaxValue)
                throw new ImmutableContentIntegrityException("The broker global frontier witness differs from the exact tail tuple, payload or full metadata.");
            return new AuthoritativeReadCut(anchor, locator.GlobalPosition + 1, clock.GetUtcNow());
        }
        catch (SourceFactUnavailableException error)
        {
            throw new ImmutableContentUnavailableException($"The source frontier witness is not available: {error.Message}");
        }
        catch (SourceFactVerificationException error)
        {
            throw new ImmutableContentIntegrityException($"The source frontier witness cannot be verified: {error.Message}");
        }
    }
}
