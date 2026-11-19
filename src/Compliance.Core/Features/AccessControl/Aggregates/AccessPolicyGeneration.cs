using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>Owns the one current platform access-policy generation and content identity.</summary>
/// <remarks>
/// The caller must qualify the exact immutable content, activation authority, deployment writer
/// barrier and source head before invoking this owner. A fact recorded here proves none of those
/// properties. Only one selected historical operation is retained for exact retry; the stream
/// remains low-churn but grows with activations. Physical expected-position CAS is required.
/// </remarks>
public sealed class AccessPolicyGeneration : Aggregate
{
    /// <summary>Provisional maximum byte length of one canonical policy artifact.</summary>
    public const long MaximumContentBytes = 16 * 1024;
    const int MaximumEncodedTextBytes = 4096;
    static readonly UTF8Encoding StrictUtf8 = new(false, true);
    readonly Uuid _selectedOperationId;
    AccessPolicyActivated? _selected;

    /// <summary>Opens the platform owner and selects one operation for exact retry.</summary>
    public AccessPolicyGeneration(Uuid selectedOperationId = default) : base(Identity, Address)
    {
        _selectedOperationId = selectedOperationId;
        On<AccessPolicyActivated>(Apply);
    }

    /// <summary>Deterministic platform policy owner identity.</summary>
    public static Uuid Identity => AggregateIdentity.Create(nameof(AccessPolicyGeneration), "platform");

    /// <summary>Exact platform policy owner stream.</summary>
    public static EventStreamAddress Address => new("bdgrz", "access-policy-generation", Identity.ToString());

    /// <summary>The active generation, or zero before bootstrap.</summary>
    public ulong Generation { get; private set; }

    /// <summary>The active immutable content reference, or null before bootstrap.</summary>
    public ImmutableContentRef? Content { get; private set; }

    /// <summary>The exact selected committed or pending activation, if one was found.</summary>
    public AccessPolicyActivated? SelectedActivation => _selected;

    /// <summary>Activates a strictly newer generation at the originally observed physical position.</summary>
    public Result Activate(Uuid operationId, ulong expectedPosition, ulong generation, ImmutableContentRef content,
        ActorAttribution actor)
    {
        if (operationId == Uuid.Empty || operationId != _selectedOperationId || generation == 0 || !Valid(content, actor))
            return Failure(RequestErrorKind.Validation, "The exact operation and bounded policy content reference are required.");
        if (_selected is { } prior)
            return prior.OperationId == operationId && prior.ExpectedPosition == expectedPosition && prior.Generation == generation &&
                prior.Content == content && prior.Actor == actor ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The activation operation was already bound to different intent.");
        if (expectedPosition != CommittedStreamPosition || expectedPosition != Version || generation <= Generation)
            return Failure(RequestErrorKind.Conflict, "The exact source position and a newer policy generation are required.");
        RaiseEvent(new AccessPolicyActivated(operationId, expectedPosition, generation, content, actor));
        return Result.Success;
    }

    void Apply(AccessPolicyActivated fact)
    {
        if (fact.OperationId == Uuid.Empty || fact.ExpectedPosition != Version || fact.Generation <= Generation || !Valid(fact.Content, fact.Actor) ||
            fact.OperationId == _selectedOperationId && _selected is not null)
            throw new InvalidOperationException("The policy activation history is invalid.");
        Generation = fact.Generation;
        Content = fact.Content;
        if (fact.OperationId == _selectedOperationId)
            _selected = fact;
    }

    static bool Valid(ImmutableContentRef? content, ActorAttribution? actor)
    {
        if (content is null || actor is null || content.ContentId == Uuid.Empty ||
            content.Bytes is <= 0 or > MaximumContentBytes ||
            content.Sha256 is not { Length: 64 } ||
            !content.Sha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            return false;
        var remaining = MaximumEncodedTextBytes;
        return Consume(content.Schema, ref remaining) && Consume(content.MediaType, ref remaining) &&
            Consume(actor.Subject, ref remaining) && Consume(actor.Issuer, ref remaining);
    }

    static bool Consume(string? value, ref int remaining)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > remaining)
            return false;
        try
        {
            if (StrictUtf8.GetByteCount(value) > remaining)
                return false;
            var length = JsonEncodedText.Encode(value).EncodedUtf8Bytes.Length;
            if (length > remaining)
                return false;
            remaining -= length;
            return true;
        }
        catch (EncoderFallbackException) { return false; }
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
