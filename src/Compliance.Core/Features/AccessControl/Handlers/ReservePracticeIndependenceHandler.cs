using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The practice subject pair of the generic acquire receiver: it reserves one staff member or canonical user
///     temporarily and exclusively for the frozen practice. The reservation derives from the original root admission
///     and does not wait for any other grant. A subject that permanently serves the opposite practice rejects the
///     operation and fences the key through the abort path; another operation holding the opposite practice only
///     makes the reservation wait.
/// </summary>
sealed class ReservePracticeIndependenceHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementAcceptanceVerifier verifier)
    : AuthorizeSourceUseHandler<PracticeIndependenceCommitment, ReservePracticeIndependence>(reader, executor,
        content, clock)
{
    protected override PracticeIndependenceCommitment Open(ReservePracticeIndependence request,
        Uuid selectedCommandId) =>
        new(EngagementPersonalDecisionVerifier.TenantOf(request.Target), request.SubjectKind, request.SubjectId,
            selectedCommandId, request.Practice, EngagementOf(request.Source));

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(ReservePracticeIndependence request,
        CancellationToken ct)
    {
        var verified = await verifier.AuthorizeAsync(request.Source, request.Intent, request.OperationId,
            Uuid.Empty, request.CommandId, request.Target, request.Generation, nameof(ReservePracticeIndependence),
            PinnedUse.None, ct).ConfigureAwait(false);
        if (!verified.IsSuccess || verified.Value!.SupersededReason is not null)
            return verified;
        try
        {
            var subject = await Content.ReadJsonAsync<PracticeSubjectIntent>(request.Target.Realm,
                verified.Value.Requirement.RequiredFacts, ct).ConfigureAwait(false);
            if (subject.Kind != request.SubjectKind || subject.SubjectId != request.SubjectId ||
                subject.Practice != request.Practice || subject.Stream != request.Target)
                return Result<SourceUseSource>.Failure(EngagementAcceptanceVerifier.Permanent(
                    "The reservation differs from the frozen practice subject."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<SourceUseSource>.Failure(EngagementAcceptanceVerifier.Permanent(
                $"The frozen practice subject cannot be verified: {exception.Message}"));
        }

        return verified;
    }

    protected override ValueTask<Result> VerifyPinnedAsync(PracticeIndependenceCommitment owner,
        SourceUseRequirement requirement, CancellationToken ct) =>
        ValueTask.FromResult(IsPinned(owner, requirement)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The subject permanently serves the opposite practice for this client.", false)));

    protected override bool IsPinned(PracticeIndependenceCommitment owner, SourceUseRequirement requirement) =>
        owner.Practice is null || owner.Practice == owner.IntendedPractice;

    protected override Func<PracticeIndependenceCommitment, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (PracticeIndependenceCommitment owner, OperationProvenance provenance, ImmutableContentRef intent,
            long generation) => owner.Reserve(provenance, intent, generation);

    protected override ValueTask<Result<ReceiverOutcome>> RefuseAsync(ReservePracticeIndependence request,
        SourceUseSource source, string reason, IExecutionContext context, CancellationToken ct) =>
        ValueTask.FromResult(Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason, null)));

    static Uuid EngagementOf(NativeFactReference source) =>
        Uuid.TryParse(source.Stream.Resource, CultureInfo.InvariantCulture, out var engagementId)
            ? engagementId
            : Uuid.Empty;
}
