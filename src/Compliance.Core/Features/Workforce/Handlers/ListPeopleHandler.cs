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
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ListPeopleHandler(IPersonDirectoryReader directory,
    PersonReadConsistency consistency) : IRequestHandler<ListPeople, Page<PersonView>>
{
    public async ValueTask<Result<Page<PersonView>>> HandleAsync(
        IRequestContext<ListPeople> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<PersonView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The person list limit must be between 1 and 200."));
        var ready = await consistency.CaptureListFenceAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<PersonView>>.Failure(ready.Error);
        Page<PersonView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<PersonView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The person cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId))
            return Result<Page<PersonView>>.Failure(new RequestError(RequestErrorKind.NotFound, "The people were not found."));
        var verified = new List<PersonView>();
        foreach (var item in page.Items)
        {
            var source = await consistency.GetAsync(request.TenantId, item.PersonId, item.Revision, ct).ConfigureAwait(false);
            if (!source.IsSuccess)
                return Result<Page<PersonView>>.Failure(source.Error!);
            verified.Add(source.Value with { PersonalContact = null, RestrictedFieldsRedacted = true });
        }
        var held = await consistency.EnsureFenceHeldAsync(request.TenantId, ready.Value, ct).ConfigureAwait(false);
        return held.IsSuccess
            ? Result<Page<PersonView>>.Success(new Page<PersonView>(verified, page.NextCursor))
            : Result<Page<PersonView>>.Failure(held.Error!);
    }
}
