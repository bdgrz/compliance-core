using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Lists the program's Type I entry decisions from the program readiness owner, newest first.</summary>
public sealed class ListTypeIEntryDecisionsHandler(ReadinessReader readiness)
    : IRequestHandler<ListTypeIEntryDecisions, Page<TypeIEntryDecisionView>>
{
    public async ValueTask<Result<Page<TypeIEntryDecisionView>>> HandleAsync(
        IRequestContext<ListTypeIEntryDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        try
        {
            var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
            var decisions = await readiness.ReadEntryDecisionsAsync(request.TenantId, program, ct).ConfigureAwait(false);
            return ControlActivationSource.Paginate(decisions, request.Limit, request.Cursor, "Type I entry decisions");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<TypeIEntryDecisionView>>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
        }
    }
}
