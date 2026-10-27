using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

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
