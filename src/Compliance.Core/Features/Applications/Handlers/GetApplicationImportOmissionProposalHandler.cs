using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class GetApplicationImportOmissionProposalHandler(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
    : IRequestHandler<GetApplicationImportOmissionProposal, ApplicationImportOmissionProposalView>
{
    public async ValueTask<Result<ApplicationImportOmissionProposalView>> HandleAsync(
        IRequestContext<GetApplicationImportOmissionProposal> context, CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, content, events, request.TenantId,
            request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportOmissionProposalView>.Failure(loaded.Error);
        var (batch, source, _) = loaded.Value;
        // A proposal is current only while the batch and its source are exactly as they were when it was sealed.
        ApplicationImportOmissionProposalContent? proposal = null;
        if (batch.HasCurrentProposal && batch.State == "preview_ready")
        {
            try
            {
                proposal = await ApplicationImportContent.ReadAsync<ApplicationImportOmissionProposalContent>(content,
                    request.TenantId, batch.Proposal!.Proposal, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException)
            {
                return Result<ApplicationImportOmissionProposalView>.Failure(ApplicationImportOmissionSources.Changed());
            }
        }

        if (proposal is null || proposal.SourcePosition != source.Source.CommittedStreamPosition ||
            proposal.ContentSha256 != batch.ContentDigest)
            return Result<ApplicationImportOmissionProposalView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import has no still-current durable omission proposal.", isTransient: true));
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, content, events, request.TenantId, batch, source, ct)
                .ConfigureAwait(false))
            return Result<ApplicationImportOmissionProposalView>.Failure(ApplicationImportOmissionSources.Changed());
        return Result<ApplicationImportOmissionProposalView>.Success(new(request.TenantId, batch.Id, proposal.SourceKey,
            proposal.SourceNamespace, batch.Proposal!.Revision, proposal.SourcePosition, proposal.ContentSha256,
            Array.AsReadOnly(proposal.PresentSourceRecordIds.ToArray()), Array.AsReadOnly(proposal.Rows.ToArray()),
            proposal.MemberId, proposal.MemberDisplay, proposal.Reason, proposal.PreparedAt,
            batch.Proposal.ProposalSha256, ApplicationImportOmissionSources.Blockers));
    }
}
