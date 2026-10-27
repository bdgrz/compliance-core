using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class CreateControlDraftHandler(ControlOperations operations, IAggregateReader reader,
    IControlApplicabilityReferenceValidator applicability) : IRequestHandler<CreateControlDraft, ControlRegistration>
{
    public ValueTask<Result<ControlRegistration>> HandleAsync(IRequestContext<CreateControlDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var identifier = ControlDraft.NormalizeIdentifier(request.Identifier);
        var id = ControlDraft.IdFor(request.TenantId, request.ProgramId, identifier);
        var authored = request with
        {
            Identifier = identifier,
            Content = ControlRules.ValidateContent(request.Content) is null ? ControlRules.NormalizeContent(request.Content) : request.Content
        };
        return operations.ExecuteAsync<CreateControlDraft, ControlRegistration>(context, request.TenantId, request.ProgramId,
            id, id, nameof(CreateControlDraft), null,
            async (snapshot, actor, at, token) =>
            {
                if (ControlDraft.ValidateCreateInput(identifier, request.Content) is { } structural)
                    return Result.Failure(structural);
                var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token)
                    .ConfigureAwait(false);
                return !program.IsCreated ? Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."))
                    : await applicability.ValidateAsync(request.TenantId, request.Content, token).ConfigureAwait(false);
            },
            (rules, actor, at) =>
            {
                var created = rules.Create(request.ProgramId, id, identifier, request.Content, actor.MemberId, actor.Display, at);
                return created.IsSuccess ? Result.Success : Result.Failure(created.Error!);
            }, null, null, ct, AuthoredIntentDigest.Compute(authored));
    }
}
