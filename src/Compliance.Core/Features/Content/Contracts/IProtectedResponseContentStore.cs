using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Content.Contracts;

/// <summary>Persistent, scoped, authenticated encryption of confidential original responses.</summary>
public interface IProtectedResponseContentStore
{
    /// <summary>Fails closed when no configured active persistent encryption key is available.</summary>
    void RequireConfigured();

    /// <summary>Publishes a fresh encrypted candidate and verifies it before returning its opaque reference.</summary>
    Task<ProtectedResponseContentRef> WriteVerifiedAsync(ProtectedResponseScope scope, string schema,
        ReadOnlyMemory<byte> plaintext, CancellationToken ct = default);

    /// <summary>Authenticates the exact scope, schema, envelope and configured key before returning plaintext.</summary>
    Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(ProtectedResponseScope scope, ProtectedResponseContentRef reference,
        CancellationToken ct = default);
}
