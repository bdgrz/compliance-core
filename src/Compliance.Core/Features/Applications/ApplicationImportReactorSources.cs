using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Verifies the exact committed typed trigger, including its actual physical source position, before any reaction.</summary>
static class ApplicationImportReactorSources
{
    public static async Task VerifyAsync<TEvent>(ISourceFactResolver resolver, IImmutableContentStore content,
        EventStreamPattern pattern, IReactorContext<TEvent> context, EventStreamAddress expected,
        CancellationToken ct) where TEvent : DomainEvent
    {
        try
        {
            if (pattern.IsTenantTemplate || pattern.Realm != expected.Realm || context.Source.Stream != expected ||
                context.Trigger is not INativeOperationFact native || native.Operation.Owner != expected ||
                context.Source.Event.GetType() != typeof(TEvent) || context.Trigger.Metadata.IsAudit ||
                !Uuid.TryParse(expected.Resource, CultureInfo.InvariantCulture, out var aggregateId) ||
                context.Trigger.Metadata.AggregateId != aggregateId)
                throw new ImmutableContentIntegrityException("The application import reaction has another exact tenant or native source owner.");
            var (_, actual, _) = await SourceFactProofs.BuildAsync<TEvent>(resolver, content, expected.Realm,
                NativeFactReference.Of(expected, context.Trigger), context.Source.ResourceOffset, ct).ConfigureAwait(false);
            if (actual.Metadata != context.Trigger.Metadata || actual.Metadata != context.Source.Event.Metadata ||
                ContentDigest.Sha256(actual, typeof(TEvent)) != ContentDigest.Sha256(context.Trigger, typeof(TEvent)) ||
                ContentDigest.Sha256(actual, typeof(TEvent)) != ContentDigest.Sha256(context.Source.Event, typeof(TEvent)))
                throw new ImmutableContentIntegrityException("The application import trigger differs from its exact committed typed source bytes.");
        }
        catch (SourceFactVerificationException error)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The exact application import source cannot be verified: {error.Message}", error is SourceFactUnavailableException));
        }
        catch (ImmutableContentIntegrityException error)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The exact application import bytes cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }
}
