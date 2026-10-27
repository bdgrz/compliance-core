using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>Compares the evaluation projection with its event-sourced source revision.</summary>
public sealed class RiskEvaluationReadConsistency(IRiskEvaluationDirectoryReader directory,
    IAggregateReader reader, RiskEvaluationReader evaluations)
{
    public async ValueTask<Result<RiskEvaluationView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid riskId, long? minimumRevision, CancellationToken ct)
    {
        try
        {
            if (minimumRevision is < 0)
                return Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The minimum risk evaluation revision cannot be negative."));
            var risk = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
                .ConfigureAwait(false);
            if (!risk.IsCreated || risk.ProgramId != programId)
                return Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The risk was not found."));
            var source = await evaluations.ReadAsync(tenantId, programId, riskId, ct).ConfigureAwait(false);
            if (minimumRevision is { } minimum && source.Revision < minimum)
                return Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The risk evaluation source has not reached revision {minimum}.",
                    isTransient: true));
            if (source.Revision == 0)
                return Result<RiskEvaluationView>.Success(new RiskEvaluationView(tenantId, programId,
                    riskId, 0, "unassessed", [], null, [], null, null, null));
            var view = await directory.GetAsync(tenantId, riskId, ct).ConfigureAwait(false);
            return view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
                   view.RiskId == riskId && view.Revision >= source.Revision
                ? Result<RiskEvaluationView>.Success(source.ToView())
                : Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The risk evaluation projection has not reached the current source revision.",
                    isTransient: true));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<RiskEvaluationView>.Failure(RiskEvaluationReadFailure.Of(failure));
        }
    }
}
