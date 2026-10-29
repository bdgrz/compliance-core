using System.Reflection;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.SourceFacts.History;

/// <summary>
/// The persisted kind of a domain event: its wire discriminator name and version (<c>name@version</c>). C# type names
/// never enter proofs, so renaming a class cannot invalidate a stored proof; changing the wire contract does.
/// </summary>
public static class EventKind
{
    /// <summary>Returns <c>name@version</c> from the event type's <see cref="DiscriminatorAttribute"/>.</summary>
    public static string Of(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        var discriminator = eventType.GetCustomAttribute<DiscriminatorAttribute>()
            ?? throw new InvalidOperationException($"{eventType.FullName} declares no [Discriminator].");
        return $"{discriminator.Name}@{discriminator.Version}";
    }

    /// <summary>Returns the kind of a concrete event instance.</summary>
    public static string Of(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Of(domainEvent.GetType());
    }
}
