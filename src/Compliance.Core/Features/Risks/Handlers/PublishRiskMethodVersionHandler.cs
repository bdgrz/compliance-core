using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Resolves an exact accepted publication before checking mutable program or version state.</summary>
public sealed class PublishRiskMethodVersionHandler(NativeOperationExecutor operations,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<PublishRiskMethodVersion, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(IRequestContext<PublishRiskMethodVersion> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.ExpectedVersion < 0)
            return Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A method publication requires its tenant, program and original non-negative version."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var command = ((IIdentifiedRequest)request).RequestId;
        var stream = new RiskMethod(request.TenantId, request.ProgramId).Stream;
        var provenance = RootOperation.Provenance(command, nameof(PublishRiskMethodVersion), stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), request.ExpectedVersion,
            AuthoredIntentDigest.Compute(request), clock.GetUtcNow()) with
        { FirstOwner = stream };
        ImmutableContentRef? body = null;
        ImmutableContentRef? result = null;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<RiskMethod>(AcceptedOperationBinding.Of(provenance),
                () => new RiskMethod(request.TenantId, request.ProgramId, command), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var gate = owner.PreparePublish(request.ExpectedVersion);
                    if (!gate.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(gate.Error!);
                    var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token).ConfigureAwait(false);
                    if (!program.IsCreated)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
                    var rules = new RiskMethodRules(request.TenantId, request.ProgramId);
                    var validation = rules.Publish(request.ProgramId, 0, request.LikelihoodScale, request.ImpactScale,
                        request.AppetiteThreshold, provenance.Actor, provenance.AcceptedAt);
                    if (validation is not null)
                        return Result<FirstResponseDescriptor>.Failure(validation.ToRequestError());
                    var version = rules.Current! with
                    {
                        Version = request.ExpectedVersion + 1,
                        MethodVersionId = Uuid.CreateVersion5(owner.Id,
                            (request.ExpectedVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    };
                    body = await NativeContent.WriteAsync(content, request.TenantId, RiskMethodContent.Schema, new RiskMethodPublicationContent(request, version), token).ConfigureAwait(false);
                    var frozen = await NativeContent.FreezeAsync(content, request.TenantId, RiskMethodContent.ResultSchema, version, token).ConfigureAwait(false);
                    result = frozen.Result;
                    return Result<FirstResponseDescriptor>.Success(frozen.Response);
                }, (owner, first) => owner.Append(provenance, body!, first, result!)), context, ct).ConfigureAwait(false);
            return await NativeContent.ResponseAsync<RiskMethodVersionView>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk method content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
