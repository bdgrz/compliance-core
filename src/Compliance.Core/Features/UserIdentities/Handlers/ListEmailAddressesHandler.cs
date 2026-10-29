using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

public sealed class ListEmailAddressesHandler(IEmailAddressDirectoryReader directory)
    : IRequestHandler<ListEmailAddresses, Page<EmailAddressView>>
{
    public async ValueTask<Result<Page<EmailAddressView>>> HandleAsync(
        IRequestContext<ListEmailAddresses> context, CancellationToken ct)
    {
        if (context.Request.Limit is < 1 or > 200)
            return Result<Page<EmailAddressView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Limit must be between 1 and 200."));

        try
        {
            return Result<Page<EmailAddressView>>.Success(await directory.ListAsync(
                context.Request.UserId, context.Request.Limit, context.Request.Cursor, ct).ConfigureAwait(false));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<EmailAddressView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The email address cursor is invalid."));
        }
    }
}
