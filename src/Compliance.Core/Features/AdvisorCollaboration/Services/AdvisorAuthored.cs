using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Services;

/// <summary>Complete canonical authored values referenced by advisor native business facts.</summary>
static class AdvisorAuthored
{
    public static string Schema<T>() => typeof(T).Name + ".AuthoredContent.v1";

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId,
        T request, CancellationToken ct) where T : notnull =>
        AdvisorContent.WriteAsync(content, tenantId, Schema<T>(), request, ct);

    static async Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        OperationProvenance operation, CancellationToken ct) where T : notnull
    {
        if (reference is null || operation is null || reference.Schema != Schema<T>() || operation.CommandKind != typeof(T).Name)
            throw Invalid("The advisor authored content has another command schema.");
        var request = await AdvisorContent.ReadAsync<T>(content, tenantId, reference, ct).ConfigureAwait(false);
        if (AuthoredIntentDigest.Compute(request) != operation.AuthoredIntentSha256)
            throw Invalid("The advisor authored content differs from its exact native intent.");
        return request;
    }
    public static async Task<CreateAdvisorHandoff> ReadAsync(IImmutableContentStore content, AdvisorHandoffCreated value,
        CancellationToken ct)
    {
        var request = await ReadAsync<CreateAdvisorHandoff>(content, value.TenantId, value.Authored, value.Operation, ct).ConfigureAwait(false);
        if (!(value.TenantId == request.TenantId &&
            value.ProgramId == request.ProgramId &&
            value.HandoffId == request.HandoffId &&
            value.ExpiresOn == request.ExpiresOn &&
            value.ComparedToHandoffId == request.ComparedToHandoffId &&
            value.Operation.LogicalId == request.HandoffId &&
            value.Operation.OriginalExpectedRevision is null &&
            Text(request.Purpose) &&
            AdvisorHandoffRules.ValidSelection(request.RecordVersions) &&
            Root(value.Operation, HandoffStream(value.TenantId, value.HandoffId), value.CreatedBy, value.CreatedAt)))
            throw Invalid("The advisor authored command has another native tuple or original terms.");
        return request;
    }

    public static async Task<ReviseAdvisorHandoffSelection> ReadAsync(IImmutableContentStore content, AdvisorHandoffSelectionRevised value,
        CancellationToken ct)
    {
        var request = await ReadAsync<ReviseAdvisorHandoffSelection>(content, value.TenantId, value.Authored, value.Operation, ct).ConfigureAwait(false);
        if (!(value.TenantId == request.TenantId &&
            value.ProgramId == request.ProgramId &&
            value.HandoffId == request.HandoffId &&
            value.Operation.LogicalId == request.CommandId &&
            request.ExpectedRevision >= 0 &&
            value.Operation.OriginalExpectedRevision == (ulong)request.ExpectedRevision &&
            value.Revision == request.ExpectedRevision + 1 &&
            Text(request.ChangeReason) &&
            AdvisorHandoffRules.ValidSelection(request.RecordVersions) &&
            Root(value.Operation, HandoffStream(value.TenantId, value.HandoffId), value.RevisedBy, value.RevisedAt)))
            throw Invalid("The advisor authored command has another native tuple or original terms.");
        return request;
    }

    public static async Task<CloseAdvisorHandoff> ReadAsync(IImmutableContentStore content, AdvisorHandoffClosed value,
        CancellationToken ct)
    {
        var request = await ReadAsync<CloseAdvisorHandoff>(content, value.TenantId, value.Authored, value.Operation, ct).ConfigureAwait(false);
        if (!(value.TenantId == request.TenantId &&
            value.ProgramId == request.ProgramId &&
            value.HandoffId == request.HandoffId &&
            value.Operation.LogicalId == request.CommandId &&
            request.ExpectedRevision >= 0 &&
            value.Operation.OriginalExpectedRevision == (ulong)request.ExpectedRevision &&
            value.Revision == request.ExpectedRevision + 1 &&
            Text(request.Rationale) &&
            Root(value.Operation, HandoffStream(value.TenantId, value.HandoffId), value.ClosedBy, value.ClosedAt)))
            throw Invalid("The advisor authored command has another native tuple or original terms.");
        return request;
    }

    public static async Task<RevokeAdvisorHandoff> ReadAsync(IImmutableContentStore content, AdvisorHandoffRevoked value,
        CancellationToken ct)
    {
        var request = await ReadAsync<RevokeAdvisorHandoff>(content, value.TenantId, value.Authored, value.Operation, ct).ConfigureAwait(false);
        if (!(value.TenantId == request.TenantId &&
            value.ProgramId == request.ProgramId &&
            value.HandoffId == request.HandoffId &&
            value.Operation.LogicalId == request.CommandId &&
            request.ExpectedRevision >= 0 &&
            value.Operation.OriginalExpectedRevision == (ulong)request.ExpectedRevision &&
            value.Revision == request.ExpectedRevision + 1 &&
            Text(request.Rationale) &&
            Root(value.Operation, HandoffStream(value.TenantId, value.HandoffId), value.RevokedBy, value.RevokedAt)))
            throw Invalid("The advisor authored command has another native tuple or original terms.");
        return request;
    }

    public static async Task<CorrectAdvisorFeedback> ReadAsync(IImmutableContentStore content, AdvisorFeedbackCorrected value,
        CancellationToken ct)
    {
        var request = await ReadAsync<CorrectAdvisorFeedback>(content, value.TenantId, value.Authored, value.Operation, ct).ConfigureAwait(false);
        if (!(value.TenantId == request.TenantId &&
            value.ProgramId == request.ProgramId &&
            value.HandoffId == request.HandoffId &&
            value.FeedbackId == request.FeedbackId &&
            value.CorrectionId == request.CorrectionId &&
            value.Operation.LogicalId == request.CorrectionId &&
            request.ExpectedRevision >= 0 &&
            value.Operation.OriginalExpectedRevision == (ulong)request.ExpectedRevision &&
            Text(request.CorrectedBody) &&
            Text(request.CorrectionReason) &&
            Root(value.Operation, ThreadStream(value.TenantId, value.ProgramId, value.HandoffId, value.FeedbackId), value.CorrectedBy, value.CorrectedAt)))
            throw Invalid("The advisor authored command has another native tuple or original terms.");
        return request;
    }

    public static async Task<RespondToAdvisorFeedback> ReadAsync(IImmutableContentStore content, AdvisorFeedbackResponded value,
        CancellationToken ct)
    {
        var request = await ReadAsync<RespondToAdvisorFeedback>(content, value.TenantId, value.Authored, value.Operation, ct).ConfigureAwait(false);
        if (!(value.TenantId == request.TenantId &&
            value.ProgramId == request.ProgramId &&
            value.HandoffId == request.HandoffId &&
            value.FeedbackId == request.FeedbackId &&
            value.Operation.LogicalId == request.CommandId &&
            request.ExpectedRevision >= 0 &&
            value.Operation.OriginalExpectedRevision == (ulong)request.ExpectedRevision &&
            Text(request.Response) &&
            Root(value.Operation, ThreadStream(value.TenantId, value.ProgramId, value.HandoffId, value.FeedbackId), value.RespondedBy, value.RespondedAt)))
            throw Invalid("The advisor authored command has another native tuple or original terms.");
        return request;
    }

    static bool Root(OperationProvenance operation, EventStreamAddress owner, ActorReference actor, DateTimeOffset at) =>
        operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null &&
        operation.Owner == owner && (operation.FirstOwner ?? operation.Owner) == owner &&
        operation.AffectedOwner == owner && operation.ExpectedRevisionOwner == owner &&
        operation.Episode == AdvisorContent.InitialEpisode && operation.Actor == actor &&
        operation.AcceptedAt == at && at != default && AdvisorHandoffRules.ValidActor(actor);

    static EventStreamAddress HandoffStream(Uuid tenant, Uuid handoff) =>
        new(tenant.ToString(), AdvisorHandoff.Area, handoff.ToString());

    static EventStreamAddress ThreadStream(Uuid tenant, Uuid program, Uuid handoff, Uuid feedback) =>
        new(tenant.ToString(), AdvisorFeedbackThread.Area,
            AdvisorFeedbackThread.IdentityOf(tenant, program, handoff, feedback).ToString());

    static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
