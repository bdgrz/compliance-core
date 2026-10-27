using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class CreateCommitmentDraftHandler(CommitmentOperations operations, ProviderSource providers)
    : IRequestHandler<CreateCommitmentDraft, CommitmentDraftRegistration>
{
    public ValueTask<Result<CommitmentDraftRegistration>> HandleAsync(IRequestContext<CreateCommitmentDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var kind = CommitmentDraft.NormalizeKind(request.Kind);
        var identifier = CommitmentDraft.NormalizeIdentifier(request.Identifier);
        if (request.DraftId == Uuid.Empty || request.DraftId != CommitmentDraft.IdFor(request.TenantId, request.ProgramId, kind, identifier))
            return ValueTask.FromResult(Result<CommitmentDraftRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "DraftId must match the commitment kind and identifier.")));
        if (request.ProviderId == Uuid.Empty || kind == "subservice_responsibility" && request.ProviderId is null ||
            kind != "subservice_responsibility" && request.ProviderId is not null)
            return ValueTask.FromResult(Result<CommitmentDraftRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A CSOC requires a subservice provider; other commitment kinds cannot reference a provider.")));
        return operations.ExecuteAsync<CreateCommitmentDraft, CommitmentDraftRegistration>(context, request.TenantId, request.ProgramId,
            request.DraftId, request.DraftId, nameof(CreateCommitmentDraft), null,
            async (_, _, _, token) =>
            {
                if (request.ProviderId is { } providerId)
                {
                    var provider = await providers.GetAsync(request.TenantId, providerId, token).ConfigureAwait(false);
                    if (provider is null || !provider.Content.Subservice)
                        return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The subservice provider was not found in this tenant."));
                }
                var service = await operations.Sources.HydrateAsync(new ClientService(request.TenantId, request.ServiceId), token).ConfigureAwait(false);
                return service.IsActive && service.ProgramId == request.ProgramId ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program service was not found."));
            },
            (rules, actor, at) =>
            {
                var result = rules.Create(request.ProgramId, request.DraftId, request.ServiceId, kind, identifier, request.Statement,
                    request.Context, request.SourceReference, actor.MemberId, actor.Display, at, request.ProviderId);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
            }, null, null, ct, authoredDigest: CommitmentCreateIntent.Digest(request));
    }
}
