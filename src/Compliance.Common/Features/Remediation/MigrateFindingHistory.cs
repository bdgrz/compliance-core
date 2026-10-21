using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Starts or advances one bounded step of the fenced legacy finding migration. A missing
///     source offset starts the migration; a supplied offset must equal the persisted cursor.
/// </summary>
[Discriminator("bdgrz.remediation.migrate-finding-history", 1)]
public sealed record MigrateFindingHistory(Uuid TenantId, Uuid ProgramId, Uuid MigrationId,
    ulong? ExpectedSourceOffset = null) : IRequest, IProgramScopedRequest;
