using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ListTeamMembersHandler(ITeamMemberDirectoryReader directory,
    IDomainEventReader? events = null, ITenantMembershipDirectoryReader? memberships = null,
    IEmailAddressDirectoryReader? emails = null, IUserDisplayNameReader? profiles = null,
    IPersonMemberDisplayReader? people = null)
    : IRequestHandler<ListTeamMembers, Page<TeamMemberView>>
{
    public async ValueTask<Result<Page<TeamMemberView>>> HandleAsync(
        IRequestContext<ListTeamMembers> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team member list limit must be between 1 and 200."));
        Page<TeamMemberView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.TeamId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team member cursor is invalid."));
        }
        if (page.Items.Any(item => item.TeamId != request.TeamId))
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The team members were not found."));
        if (events is null || memberships is null)
            return Result<Page<TeamMemberView>>.Success(page);
        var enriched = new List<TeamMemberView>(page.Items.Count);
        foreach (var member in page.Items)
        {
            var result = await MemberPresentationEnrichment.WithTeamAsync(events, memberships,
                emails, profiles, people, request.TenantId, member, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result<Page<TeamMemberView>>.Failure(result.Error);
            enriched.Add(result.Value);
        }
        return Result<Page<TeamMemberView>>.Success(new Page<TeamMemberView>(enriched, page.NextCursor));
    }
}
