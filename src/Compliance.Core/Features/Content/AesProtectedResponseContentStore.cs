using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Content;

/// <summary>
/// AES-256-GCM envelopes on the persistent immutable content adapter. The digest covers the complete versioned
/// header, fresh 96-bit nonce, ciphertext and 128-bit tag. The authenticated data binds the header, object identity,
/// realm, affected owner, root operation, original actor identity and result schema. No plaintext is persisted.
/// </summary>
public sealed class AesProtectedResponseContentStore(IImmutableContentStore content, ProtectedResponseKeyRing keys)
    : IProtectedResponseContentStore
{
    internal const string EnvelopeSchema = "protected-response-aes256gcm.v1";
    internal const string EnvelopeMediaType = "application/vnd.bdgrz.protected-response";
    internal const int MaximumPlaintextBytes = 1024 * 1024;
    const int NonceBytes = 12;
    const int TagBytes = 16;
    static readonly byte[] Magic = "BDGRZPR1"u8.ToArray();
    static readonly UTF8Encoding Utf8 = new(false, true);

    public void RequireConfigured()
    {
        if (keys.ActiveKeyVersionId is null)
            throw new ImmutableContentUnavailableException("Protected response encryption is not configured.");
    }

    public async Task<ProtectedResponseContentRef> WriteVerifiedAsync(ProtectedResponseScope scope, string schema,
        ReadOnlyMemory<byte> plaintext, CancellationToken ct = default)
    {
        Validate(scope, schema);
        RequireConfigured();
        if (plaintext.Length > MaximumPlaintextBytes)
            throw new ArgumentOutOfRangeException(nameof(plaintext), "The protected response exceeds its size limit.");
        var id = Uuid.CreateVersion4();
        var version = keys.ActiveKeyVersionId!;
        var envelope = Encrypt(scope, schema, id, version, keys.Key(version)!, plaintext.Span);
        var written = await content.WriteVerifiedAsync(scope.Realm, id, EnvelopeSchema, EnvelopeMediaType,
            envelope, ct).ConfigureAwait(false);
        if (written.ContentId != id || written.Schema != EnvelopeSchema || written.MediaType != EnvelopeMediaType ||
            written.Bytes != envelope.Length || written.Sha256 != ContentDigest.Sha256(envelope))
            throw new ImmutableContentIntegrityException("The protected response publication did not verify.");
        var reference = new ProtectedResponseContentRef(id, written.Sha256, written.Bytes, schema, version);
        var verified = await ReadVerifiedAsync(scope, reference, ct).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(plaintext.Span, verified.Span))
            throw new ImmutableContentIntegrityException("The protected response round-trip did not verify.");
        return reference;
    }

    public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(ProtectedResponseScope scope,
        ProtectedResponseContentRef reference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Validate(scope, reference.Schema);
        if (reference.ContentId == Uuid.Empty || reference.Bytes is < 40 or > MaximumPlaintextBytes + 4096 ||
            reference.CiphertextSha256 is not { Length: 64 } ||
            reference.CiphertextSha256.Any(static c => !char.IsAsciiHexDigit(c)) ||
            reference.KeyVersionId is not { Length: > 0 and <= 128 } ||
            reference.KeyVersionId.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new ImmutableContentIntegrityException("The protected response reference is invalid.");
        var bytes = await content.ReadVerifiedAsync(scope.Realm, new ImmutableContentRef(reference.ContentId,
            reference.CiphertextSha256, reference.Bytes, EnvelopeMediaType, EnvelopeSchema), ct).ConfigureAwait(false);
        // Verify again at this boundary: a broken adapter may never bypass authentication or digest/length checks.
        if (bytes.Length != reference.Bytes || !string.Equals(ContentDigest.Sha256(bytes.Span),
                reference.CiphertextSha256, StringComparison.OrdinalIgnoreCase))
            throw new ImmutableContentIntegrityException("The protected response envelope does not match its reference.");
        return Decrypt(scope, reference, bytes.ToArray());
    }

    static byte[] Encrypt(ProtectedResponseScope scope, string schema, Uuid id, string version, byte[] key,
        ReadOnlySpan<byte> plaintext)
    {
        var header = Header(id, version, schema);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Aad(scope, header));
        var envelope = new byte[header.Length + NonceBytes + ciphertext.Length + TagBytes];
        header.CopyTo(envelope, 0);
        nonce.CopyTo(envelope, header.Length);
        ciphertext.CopyTo(envelope, header.Length + NonceBytes);
        tag.CopyTo(envelope, envelope.Length - TagBytes);
        return envelope;
    }

    byte[] Decrypt(ProtectedResponseScope scope, ProtectedResponseContentRef reference, byte[] envelope)
    {
        var header = Header(reference.ContentId, reference.KeyVersionId, reference.Schema);
        if (envelope.Length < header.Length + NonceBytes + TagBytes ||
            !CryptographicOperations.FixedTimeEquals(envelope.AsSpan(0, header.Length), header))
            throw new ImmutableContentIntegrityException("The protected response envelope header is invalid.");
        var key = keys.Key(reference.KeyVersionId)
            ?? throw new ImmutableContentUnavailableException("The protected response decryption key is unavailable.");
        var plaintext = new byte[envelope.Length - header.Length - NonceBytes - TagBytes];
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(envelope.AsSpan(header.Length, NonceBytes),
                envelope.AsSpan(header.Length + NonceBytes, plaintext.Length),
                envelope.AsSpan(envelope.Length - TagBytes), plaintext, Aad(scope, header));
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new ImmutableContentIntegrityException("The protected response authentication failed.");
        }
    }

    static byte[] Header(Uuid id, string version, string schema)
    {
        using var buffer = new MemoryStream();
        buffer.Write(Magic);
        Write(buffer, id.ToString());
        Write(buffer, version);
        Write(buffer, schema);
        return buffer.ToArray();
    }

    static byte[] Aad(ProtectedResponseScope scope, byte[] header)
    {
        using var buffer = new MemoryStream();
        buffer.Write(header);
        Write(buffer, scope.Realm);
        Write(buffer, scope.AffectedOwner.Realm);
        Write(buffer, scope.AffectedOwner.Area);
        Write(buffer, scope.AffectedOwner.Resource);
        Write(buffer, scope.RootOperationId.ToString());
        Write(buffer, scope.OriginalActor.Kind);
        Write(buffer, scope.OriginalActor.Id);
        return buffer.ToArray();
    }

    static void Write(Stream stream, string value)
    {
        var bytes = Utf8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }

    static void Validate(ProtectedResponseScope scope, string schema)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(scope.Realm) || scope.Realm.Length > 256 ||
            scope.AffectedOwner is null || scope.AffectedOwner.Realm != scope.Realm ||
            scope.RootOperationId == Uuid.Empty || scope.OriginalActor is not { Kind.Length: > 0, Id.Length: > 0 } ||
            string.IsNullOrWhiteSpace(schema) || schema.Length > 256)
            throw new ArgumentException("Protected response content requires its exact bounded authority scope.", nameof(scope));
    }
}
