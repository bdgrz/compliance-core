using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.Independence.Services;

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
