using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Bounded commitment to the exact ordered native payload and immutable replay metadata.</summary>
sealed class IdentityOwnerReplayCut
{
    bool _verified;
    ulong _version;
    ulong _position;
    string _knownMetadataDigest = Empty;
    internal ulong KnownThroughVersion { get; private set; }
    internal string MetadataDigest => _knownMetadataDigest;
    public static string Empty { get; } = new('0', 64);
    public string Digest { get; private set; } = Empty;

    public void Observe(DomainEvent fact, bool emitted)
    {
        Digest = Append(Digest, fact);
        // Save attribution is added after emission. A replay observes the complete envelope; an emitted
        // suffix first acquires its durable envelope at full finalization, after matching every known prefix.
        if (!emitted && fact.Metadata.AggregateVersion == KnownThroughVersion + 1)
        {
            _knownMetadataDigest = Append(_knownMetadataDigest, fact, true);
            KnownThroughVersion++;
        }
        _verified = false;
    }

    public void Confirm(Aggregate owner, string digest, string fullMetadataDigest, string knownMetadataPrefix)
    {
        if (Digest != digest || _knownMetadataDigest != knownMetadataPrefix)
            throw new ImmutableContentIntegrityException("The identity owner differs from its complete native payload and metadata prefix.");
        (_verified, _version, _position) = (true, owner.Version, owner.CommittedStreamPosition);
        (_knownMetadataDigest, KnownThroughVersion) = (fullMetadataDigest, owner.Version);
    }

    public Result Require(Aggregate owner)
    {
        // A fresh owner may assemble its initial batch. Every committed owner must have its complete prefix finalized.
        if (owner.CommittedStreamPosition == 0)
            return Result.Success;
        return _verified && _version == owner.Version && _position == owner.CommittedStreamPosition
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity operation requires its exact finalized native owner cut.", true));
    }

    public static string Append(string previous, DomainEvent fact, bool fullMetadata = false)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Add(hash, Encoding.UTF8.GetBytes(previous));
        Add(hash, Encoding.UTF8.GetBytes(Bdgrz.Compliance.Features.SourceFacts.EventKind.Of(fact)));
        Add(hash, JsonSerializer.SerializeToUtf8Bytes(fact, ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!));
        var metadata = fact.Metadata;
        Add(hash, Encoding.UTF8.GetBytes(metadata.EventId.ToString()));
        Add(hash, Encoding.UTF8.GetBytes(metadata.AggregateId.ToString()));
        Add(hash, Encoding.UTF8.GetBytes(metadata.AggregateVersion.ToString(CultureInfo.InvariantCulture)));
        Add(hash, Encoding.UTF8.GetBytes(metadata.OccurredOn.ToString("O", CultureInfo.InvariantCulture)));
        Add(hash, [metadata.IsAudit ? (byte)1 : (byte)0]);
        if (fullMetadata)
        {
            Add(hash, Encoding.UTF8.GetBytes(metadata.CorrelationId?.ToString() ?? string.Empty));
            Add(hash, Encoding.UTF8.GetBytes(metadata.CausationId?.ToString() ?? string.Empty));
            Add(hash, Encoding.UTF8.GetBytes(metadata.ExecutionId?.ToString() ?? string.Empty));
            Add(hash, [metadata.Actor is null ? (byte)0 : (byte)1]);
            if (metadata.Actor is { } actor)
            {
                Add(hash, Encoding.UTF8.GetBytes(actor.Subject));
                Add(hash, Encoding.UTF8.GetBytes(actor.Issuer));
            }
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    static void Add(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
