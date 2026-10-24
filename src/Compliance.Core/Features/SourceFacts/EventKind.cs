using System.Reflection;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

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
