using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Closed union of verified standard or protected response content; never caller-selected.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling =
    JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(StandardResponseBody), "standard")]
[JsonDerivedType(typeof(ProtectedResponseBody), "protected")]
public abstract record ResponseBodyReference
{
    private protected ResponseBodyReference()
    {
    }
}

/// <summary>Verified standard immutable response content.</summary>
public sealed record StandardResponseBody(ImmutableContentRef Content) : ResponseBodyReference;

/// <summary>Protected response content.</summary>
public sealed record ProtectedResponseBody(ProtectedResponseContentRef Content) : ResponseBodyReference;
