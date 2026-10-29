using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class ListSystemDescriptionVersionsHandler(SystemDescriptionReader descriptions, SystemDescriptionReadConsistency consistency)
    : IRequestHandler<ListSystemDescriptionVersions, Page<SystemDescriptionVersionView>>
{
    public async ValueTask<Result<Page<SystemDescriptionVersionView>>> HandleAsync(IRequestContext<ListSystemDescriptionVersions> context, CancellationToken ct)
    {
        var r = context.Request;
        if (r.Limit is < 1 or > 200 || !SystemDescriptionVersionCursor.TryRead(r.Cursor, r.TenantId, r.ProgramId, r.SystemDescriptionId, out var cut, out var offset))
            return Fail(RequestErrorKind.Validation, "The description version limit or scoped cursor is invalid.");
        try
        {
            var read = await descriptions.ReadAsync(r.TenantId, r.ProgramId, r.SystemDescriptionId, ct).ConfigureAwait(false);
            if (read.CurrentVersion is null)
                return Fail(RequestErrorKind.NotFound, "The system description was not found.");
            if (cut is { } expected && expected != read.Owner.NativeSequence)
                return Fail(RequestErrorKind.Conflict, "The description source changed after this version page was started. Restart the list.", true);
            if (offset > read.View.Versions.Count)
                return Fail(RequestErrorKind.Validation, "The description version cursor is outside its exact history.");
            var values = read.View.Versions.Skip(offset).Take(r.Limit ?? 50).ToArray();
            var next = offset + values.Length < read.View.Versions.Count ? SystemDescriptionVersionCursor.Wrap(r.TenantId, r.ProgramId,
                r.SystemDescriptionId, read.Owner.NativeSequence, offset + values.Length) : null;
            var confirmed = await consistency.ConfirmAsync(read, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<Page<SystemDescriptionVersionView>>.Success(new(values, next)) : Result<Page<SystemDescriptionVersionView>>.Failure(confirmed.Error!);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<SystemDescriptionVersionView>>.Failure(SystemDescriptionReadFailure.Of(failure));
        }
    }
    static Result<Page<SystemDescriptionVersionView>> Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result<Page<SystemDescriptionVersionView>>.Failure(new RequestError(kind, message, transient));
}
