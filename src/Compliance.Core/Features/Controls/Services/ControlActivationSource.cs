using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>
///     Serves approved-version and decision reads from verified native source/content at an exact owner cut.
///     Discovery and presentation histories never become command-owner state.
/// </summary>
public sealed class ControlActivationSource(ControlReader controls)
{
    public const int MaximumPageSize = 200;

    public async ValueTask<Result<ControlReadSnapshot>> LoadAsync(Uuid tenantId, Uuid programId,
        Uuid controlId, CancellationToken ct)
    {
        try
        {
            var control = await controls.ReadAsync(tenantId, programId, controlId, ct).ConfigureAwait(false);
            return control.IsVisible ? Result<ControlReadSnapshot>.Success(control)
                : Result<ControlReadSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound, "The control was not found."));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ControlReadSnapshot>.Failure(ControlReadFailure.Of(error));
        }
    }

    public static Result<ControlVersionView> Version(ControlVersionView? version, string missing) =>
        version is not null
            ? Result<ControlVersionView>.Success(version)
            : Result<ControlVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                missing));

    public static Result<Page<T>> Paginate<T>(IReadOnlyList<T> items, int? limit, string? cursor,
        string record)
    {
        if (limit is < 1 or > MaximumPageSize)
            return Result<Page<T>>.Failure(new RequestError(RequestErrorKind.Validation,
                $"The {record} list limit must be between 1 and {MaximumPageSize}."));
        var offset = 0;
        if (cursor is not null && (!int.TryParse(cursor, NumberStyles.None,
                CultureInfo.InvariantCulture, out offset) || offset < 1 || offset > items.Count))
            return Result<Page<T>>.Failure(new RequestError(RequestErrorKind.Validation,
                $"The {record} cursor is invalid."));
        var take = limit ?? 50;
        var page = items.Skip(offset).Take(take).ToArray();
        var next = offset + page.Length;
        return Result<Page<T>>.Success(new Page<T>(page,
            next < items.Count ? next.ToString(CultureInfo.InvariantCulture) : null));
    }
}
