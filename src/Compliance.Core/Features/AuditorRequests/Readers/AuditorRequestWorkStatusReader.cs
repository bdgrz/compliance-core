using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.AuditorRequests.Readers;

/// <summary>Derives status from verified request content and independent response owners.</summary>
sealed class AuditorRequestWorkStatusReader(AuditorRequestReader requests)
{
    public async ValueTask<Result<string>> ReadAsync(Uuid tenantId, Uuid programId, Uuid requestId, DateOnly asOf,
        CancellationToken ct = default)
    {
        try
        {
            var snapshot = await requests.ReadAsync(tenantId, programId, requestId, ct).ConfigureAwait(false);
            return Result<string>.Success(snapshot.View is not { } view ? "missing" :
                AuditorRequestReader.WorkStatus(snapshot.Owner, view.Responses, view.DueOn, asOf));
        }
        catch (Bdgrz.Compliance.Features.Content.Services.ImmutableContentIntegrityException error)
        {
            return Result<string>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The request work status cannot be verified: {error.Message}", error is Bdgrz.Compliance.Features.Content.Services.ImmutableContentUnavailableException));
        }
    }
}
