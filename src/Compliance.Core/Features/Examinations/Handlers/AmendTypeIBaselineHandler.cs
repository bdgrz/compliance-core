using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Handlers;

public sealed class AmendTypeIBaselineHandler(ExaminationEngagementOperations operations,
    IExaminationEngagementDirectoryReader directory, ITypeIBaselinePreviewReader previews)
    : IRequestHandler<AmendTypeIBaseline>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AmendTypeIBaseline> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationManagementActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out _, out _) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var requester))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A baseline decision requires its authenticated personal actor.")));
        if (request.ExpectedRevision <= 0 || request.BaselineId == Uuid.Empty || request.ChangedRecordIds is null || request.AffectedPackageMaterialIds is null ||

            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000 ||
            !ValidDigest(request.ExpectedPreviewSha256))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A baseline decision requires a stable identity, current revision, rationale and complete preview digest.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ExaminationId,
            request.BaselineId, nameof(AmendTypeIBaseline), request.ExpectedRevision,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedRevision, requireFrozen: true),
            async (snapshot, operation, token) =>
            {
                var projected = await directory.GetAsync(request.TenantId, request.ExaminationId, token).ConfigureAwait(false);
                if (projected is null || projected.TenantId != request.TenantId || projected.ProgramId != request.ProgramId ||
                    projected.ExaminationEngagementId != request.ExaminationId)
                    return Result<ExaminationEngagementState>.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The examination engagement was not found."));
                var source = await previews.ReadAsync(snapshot.View!, requester, token).ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<ExaminationEngagementState>.Failure(source.Error!);
                var preview = source.Value!;
                if (!StringComparer.Ordinal.Equals(preview.ContentSha256, request.ExpectedPreviewSha256))
                    return Result<ExaminationEngagementState>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The Type I baseline preview changed. Review the current preview before confirming."));
                if (snapshot.Rules.AmendBaseline(request.ExpectedRevision, request.BaselineId, preview, request.ChangedRecordIds,
                        request.AffectedPackageMaterialIds, request.Reason, ExaminationEngagement.Member(operation),
                        operation.Actor.Display, operation.AcceptedAt) is { } failure)
                    return ExaminationEngagementOperations.Failure(failure);
                return ExaminationEngagementOperations.FromRules(snapshot.Rules);
            }, ct);
    }

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(
        static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
