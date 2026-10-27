using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
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
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ListTenantMembersHandler(ITenantMembershipDirectoryReader directory,
    IEmailAddressDirectoryReader? emails = null, IUserDisplayNameReader? profiles = null,
    IPersonMemberDisplayReader? people = null)
    : IRequestHandler<ListTenantMembers, Page<TenantMembershipView>>
{
    public async ValueTask<Result<Page<TenantMembershipView>>> HandleAsync(
        IRequestContext<ListTenantMembers> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TenantMembershipView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The tenant member list limit must be between 1 and 200."));
        Page<TenantMembershipView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TenantMembershipView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The tenant member cursor is invalid."));
        }
        return page.Items.Any(member => member.TenantId != request.TenantId)
            ? Result<Page<TenantMembershipView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant members were not found."))
            : Result<Page<TenantMembershipView>>.Success(new Page<TenantMembershipView>(
                await EnrichAsync(page.Items, ct).ConfigureAwait(false), page.NextCursor));
    }

    async ValueTask<IReadOnlyList<TenantMembershipView>> EnrichAsync(
        IReadOnlyList<TenantMembershipView> members, CancellationToken ct)
    {
        var enriched = new List<TenantMembershipView>(members.Count);
        foreach (var member in members)
            enriched.Add(await MemberPresentationEnrichment.WithMemberAsync(emails, member, ct,
                profiles, people).ConfigureAwait(false));
        return enriched;
    }
}
