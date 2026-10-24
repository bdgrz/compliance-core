using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Independence;

/// <summary>
///     Creates the waiver proof resolver on first use. Its stream-backed source fact resolver cannot be constructed by
///     the production container until the shared connection publishes its stream client, so a command that never needs
///     a waiver proof must not construct it.
/// </summary>
public sealed class DeferredSeparationOfDutiesWaiverProofResolver(Func<ISeparationOfDutiesWaiverProofResolver> create)
    : ISeparationOfDutiesWaiverProofResolver
{
    ISeparationOfDutiesWaiverProofResolver? _resolver;

    /// <inheritdoc />
    public ValueTask<Result<SeparationOfDutiesWaiverProof>> ResolveAsync(SeparationOfDutiesWaiverProofScope scope,
        DateTimeOffset at, CancellationToken ct = default) => Resolver.ResolveAsync(scope, at, ct);

    /// <inheritdoc />
    public ValueTask<Result> VerifyAsync(SeparationOfDutiesWaiverProof proof, DateTimeOffset at,
        CancellationToken ct = default) => Resolver.VerifyAsync(proof, at, ct);

    ISeparationOfDutiesWaiverProofResolver Resolver => _resolver ??= create();
}
