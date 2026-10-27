using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

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
