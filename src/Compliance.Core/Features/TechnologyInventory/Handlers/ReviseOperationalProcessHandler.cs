using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Admits a revision of an operational process at its expected revision as the first native fact on the
///     process's own owner. <c>CommandId</c> is the logical identity, so an exact retry replays the original 202 after
///     any later change.
/// </summary>
public sealed class ReviseOperationalProcessHandler(NamedRecordAdmission admission,
    TechnologyInventoryReferences references)
    : IRequestHandler<ReviseOperationalProcess, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<ReviseOperationalProcess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationalProcessId == Uuid.Empty ||
            request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, operational process identity and tenant are required."));
        if (request.ExpectedRevision < 1)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected operational process revision must be positive."));
        return await admission.AdmitAsync(OperationalProcessNames.Descriptor, context,
            new NamedRecordAdmissionRequest(request.TenantId, request.OperationalProcessId, request.CommandId,
                false, request.ExpectedRevision, TechnologyInventoryActor.From(context),
                AuthoredIntentDigest.Compute(request), token => OperationalProcessContents.ResolveAsync(references,
                    request.TenantId, new OperationalProcessContent(request.Name, request.Purpose,
                        request.OwnerPersonId, request.Inputs, request.Outputs, request.Lifecycle), token)),
            (OperationalProcess record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct)
            .ConfigureAwait(false);
    }
}
