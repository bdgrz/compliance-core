using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Reads the complete attributed feedback history of one handoff. The handoff lists the feedback it admitted in
///     admission order and each admitted contribution is read from its own thread; a contribution whose thread is not
///     materialized yet is listed once its independent materialization completes. A contribution is current only while
///     its exact source version is still selected and current.
/// </summary>
public sealed class ListAdvisorFeedbackHandler(AdvisorFeedbackReader reader, AdvisorHandoffReader handoffs,
    IAdvisorRecordVersionResolver versions)
    : IRequestHandler<ListAdvisorFeedback, Page<AdvisorFeedbackView>>
{
    const int DefaultLimit = 50;
    const int MaximumLimit = 200;

    public ValueTask<Result<Page<AdvisorFeedbackView>>> HandleAsync(
        IRequestContext<ListAdvisorFeedback> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<Page<AdvisorFeedbackView>>> HandleCoreAsync(
        IRequestContext<ListAdvisorFeedback> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty)
            return Invalid("An advisor feedback read requires tenant, program, and handoff identities.");
        if (request.Limit is < 1 or > MaximumLimit)
            return Invalid("The advisor feedback list limit must be between 1 and 200.");

        Uuid? cursorId = null;
        if (request.Cursor is not null)
        {
            if (!Uuid.TryParse(request.Cursor, CultureInfo.InvariantCulture, out var parsed) ||
                parsed == Uuid.Empty)
                return Invalid("The advisor feedback cursor is invalid.");
            cursorId = parsed;
        }

        var handoff = await handoffs.ReadAsync(request.TenantId, request.ProgramId, request.HandoffId, ct)
            .ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
            return Result<Page<AdvisorFeedbackView>>.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));

        var admitted = handoff.Admitted.Select(static fact => fact.Operation.LogicalId).ToArray();
        var start = 0;
        if (cursorId is { } after)
        {
            var cursorIndex = Array.IndexOf(admitted, after);
            if (cursorIndex < 0)
                return Invalid("The advisor feedback cursor is invalid for this handoff.");
            start = cursorIndex + 1;
        }

        var limit = request.Limit ?? DefaultLimit;
        var threads = new List<AdvisorFeedbackThreadSnapshot>(limit + 1);
        for (var index = start; index < admitted.Length && threads.Count <= limit; index++)
        {
            var thread = await reader.ReadAsync(request.TenantId, request.ProgramId,
                request.HandoffId, admitted[index], default, ct).ConfigureAwait(false);
            if (thread.IsMaterialized)
                threads.Add(thread);
        }

        var hasMore = threads.Count > limit;
        var pageThreads = threads.Take(limit).ToArray();
        var currentSources = new HashSet<AdvisorRecordVersion>();
        foreach (var source in pageThreads.Select(thread => SourceOf(thread.Recorded!))
                     .Where(source => handoff.Selection.Contains(source) && source.IsSupported).Distinct())
        {
            var current = await versions.RequireCurrentAsync(request.TenantId,
                request.ProgramId, [source], ct).ConfigureAwait(false);
            if (current.IsSuccess)
                currentSources.Add(source);
            else if (current.Error.IsTransient)
                return Result<Page<AdvisorFeedbackView>>.Failure(current.Error);
        }

        var pageItems = pageThreads.Select(thread =>
        {
            var recorded = thread.Recorded!;
            var source = SourceOf(recorded);
            return new AdvisorFeedbackView(request.TenantId, request.ProgramId, thread.FeedbackId,
                request.HandoffId, recorded.RecordId, recorded.RecordVersionId, recorded.Kind, recorded.Body,
                recorded.Author, recorded.CreatedAt,
                thread.Corrections.Select(correction => new AdvisorFeedbackCorrectionView(correction.CorrectionId,
                    correction.CorrectedBody, correction.CorrectionReason, correction.CorrectedBy,
                    correction.CorrectedAt)).ToArray(),
                thread.Responses.Select(response => new AdvisorFeedbackResponseView(response.Response,
                    response.RespondedBy, response.RespondedAt)).ToArray(),
                thread.Resolved?.ResolvedAt, handoff.Selection.Contains(source) &&
                    currentSources.Contains(source),
                recorded.SelectedSource, thread.Resolved?.ResolvedBy);
        }).ToArray();
        var nextCursor = hasMore && pageItems.Length > 0
            ? pageItems[^1].AdvisorFeedbackId.ToString()
            : null;
        return Result<Page<AdvisorFeedbackView>>.Success(new Page<AdvisorFeedbackView>(pageItems, nextCursor));
    }

    static AdvisorRecordVersion SourceOf(AdvisorFeedbackRecordedContent recorded) =>
        recorded.SelectedSource ?? new AdvisorRecordVersion(recorded.RecordId, recorded.RecordVersionId);

    static Result<Page<AdvisorFeedbackView>> Invalid(string message) =>
        Result<Page<AdvisorFeedbackView>>.Failure(new RequestError(RequestErrorKind.Validation,
            message));
}
