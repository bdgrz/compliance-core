using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
namespace Bdgrz.Compliance.Features.AuditorRequests.Services;

/// <summary>Verifies the exact committed typed trigger, including its actual physical source position, before any reaction.</summary>
static class AuditorRequestReactorSources
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
                throw new ImmutableContentIntegrityException("The auditor request reaction has another exact tenant or native source owner.");
            var (_, actual, _) = await SourceFactProofs.BuildAsync<TEvent>(resolver, content, expected.Realm,
                NativeFactReference.Of(expected, context.Trigger), context.Source.ResourceOffset, ct).ConfigureAwait(false);
            if (actual.Metadata != context.Trigger.Metadata || actual.Metadata != context.Source.Event.Metadata ||
                ContentDigest.Sha256(actual, typeof(TEvent)) != ContentDigest.Sha256(context.Trigger, typeof(TEvent)) ||
                ContentDigest.Sha256(actual, typeof(TEvent)) != ContentDigest.Sha256(context.Source.Event, typeof(TEvent)))
                throw new ImmutableContentIntegrityException("The auditor request trigger differs from its exact committed typed source bytes.");
        }
        catch (SourceFactVerificationException error)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The exact auditor request source cannot be verified: {error.Message}", error is SourceFactUnavailableException));
        }
        catch (ImmutableContentIntegrityException error)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The exact auditor request bytes cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }
}
