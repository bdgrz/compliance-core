using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Services;

/// <summary>Verified complete immutable content for the two native line protocols.</summary>
public static class MappingLineContent
{
    public const string Schema = "control-mapping-line-mutation.v1";

    public static JsonElement Json<T>(T value) where T : notnull => JsonSerializer.SerializeToElement(value,
        typeof(T), ComplianceCoreJsonContext.Default);

    internal static async Task<MappingLinePrepared> PrepareAsync<TRequest>(IImmutableContentStore content,
        Aggregate owner, TRequest request, object change, object current, MappingLineHeader header,
        ImmutableContentRef result, CancellationToken ct) where TRequest : notnull =>
        await PrepareJsonAsync(content, owner, Json(request), change, current, header, result, ct).ConfigureAwait(false);

    internal static async Task<MappingLinePrepared> PrepareJsonAsync(IImmutableContentStore content, Aggregate owner,
        JsonElement authored, object change, object current, MappingLineHeader header, ImmutableContentRef result,
        CancellationToken ct)
    {
        var mutation = new MappingLineMutation(authored, JsonSerializer.SerializeToElement(change,
            change.GetType(), ComplianceCoreJsonContext.Default), JsonSerializer.SerializeToElement(current,
            current.GetType(), ComplianceCoreJsonContext.Default));
        var tenant = Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var reference = await NativeContent.WriteAsync(content, tenant, Schema, mutation, ct).ConfigureAwait(false);
        var frame = Frame(change, reference, header, null, result);
        ValidateAuthored(mutation.Authored, change);
        var replayDigest = owner switch
        {
            ControlCriterionMapping mapping => mapping.ReplayDigest,
            CriterionApplicability applicability => applicability.ReplayDigest,
            _ => throw new ImmutableContentIntegrityException("The immutable preparation belongs to another owner family."),
        };
        return new MappingLinePrepared(header.Revision - 1, owner.Version, owner.CommittedStreamPosition, replayDigest, frame);
    }

    public static async Task<(object Change, JsonElement Current)> ReadAsync(IImmutableContentStore content,
        DomainEvent frame, CancellationToken ct)
    {
        if (frame is not IMappingLineFact native || native.Content is null || native.Content.Schema != Schema)
            throw new ImmutableContentIntegrityException("The line fact lacks its declared immutable content.");
        var mutation = await NativeContent.ReadAsync<MappingLineMutation>(content, native.TenantId, native.Content, ct)
            .ConfigureAwait(false);
        if (mutation.Authored.ValueKind != JsonValueKind.Object || mutation.Change.ValueKind != JsonValueKind.Object ||
            mutation.Current.ValueKind != JsonValueKind.Object)
            throw new ImmutableContentIntegrityException("The immutable line mutation lacks complete authored/change/current objects.");
        object change;
        try
        {
            change = frame switch
            {
                ControlCriterionMappingProposed => mutation.Change.Deserialize(ComplianceCoreJsonContext.Default.ControlCriterionMappingProposedChange) ?? throw new ImmutableContentIntegrityException("The business change is JSON null."),
                ControlCriterionMappingReviewed => mutation.Change.Deserialize(ComplianceCoreJsonContext.Default.ControlCriterionMappingReviewedChange) ?? throw new ImmutableContentIntegrityException("The business change is JSON null."),
                ControlCriterionMappingRetired => mutation.Change.Deserialize(ComplianceCoreJsonContext.Default.ControlCriterionMappingRetiredChange) ?? throw new ImmutableContentIntegrityException("The business change is JSON null."),
                CriterionNotApplicableProposed => mutation.Change.Deserialize(ComplianceCoreJsonContext.Default.CriterionNotApplicableProposedChange) ?? throw new ImmutableContentIntegrityException("The business change is JSON null."),
                CriterionApplicabilityReviewed => mutation.Change.Deserialize(ComplianceCoreJsonContext.Default.CriterionApplicabilityReviewedChange) ?? throw new ImmutableContentIntegrityException("The business change is JSON null."),
                CriterionNotApplicableWithdrawn => mutation.Change.Deserialize(ComplianceCoreJsonContext.Default.CriterionNotApplicableWithdrawnChange) ?? throw new ImmutableContentIntegrityException("The business change is JSON null."),
                _ => throw new ImmutableContentIntegrityException("The line has another native fact type."),
            };
            ValidateAuthored(mutation.Authored, change);
        }
        catch (JsonException exception)
        {
            throw new ImmutableContentIntegrityException("The line mutation JSON cannot be decoded: " + exception.Message);
        }
        if (!Bound(frame, change))
            throw new ImmutableContentIntegrityException("The immutable business change has another native header binding.");
        return (change, mutation.Current);
    }

    public static void ValidateCurrent(JsonElement expected, object current)
    {
        var actual = JsonSerializer.SerializeToElement(current, current.GetType(), ComplianceCoreJsonContext.Default);
        if (!JsonElement.DeepEquals(expected, actual))
            throw new ImmutableContentIntegrityException("The immutable current authority does not match the full verified transition history.");
    }

    public static ControlCriterionMappingView Current(ControlCriterionMappingRules rules) => Current(rules.View!);

    public static ControlCriterionMappingView Current(ControlCriterionMappingView view) => view with
    { Versions = [.. view.Versions.Where(static version => version.Status is "proposed" or "accepted")] };

    public static CriterionApplicabilityView Current(CriterionApplicabilityRules rules) => Current(rules.View!);

    public static CriterionApplicabilityView Current(CriterionApplicabilityView view) => view with
    { Versions = [.. view.Versions.Where(static version => version.Status is "proposed" or "accepted")] };

    public static MappingLineHeader Header(ControlCriterionMappingRules rules) => Header(rules.View!);

    public static MappingLineHeader Header(ControlCriterionMappingView view)
    {
        var pending = view.Versions.SingleOrDefault(static value => value.Status == "proposed");
        var active = view.Versions.SingleOrDefault(static value => value.Status == "accepted");
        return new MappingLineHeader(view.Revision, view.Versions.Count, view.MappingId,
            pending is null ? null : new MappingLineSlot(pending.MappingId, pending.VersionNumber, Uuid.Parse(pending.ProposedBy.Id, System.Globalization.CultureInfo.InvariantCulture)),
            active is null ? null : new MappingLineSlot(active.MappingId, active.VersionNumber, Uuid.Parse(active.ProposedBy.Id, System.Globalization.CultureInfo.InvariantCulture)),
            view.Versions.Any(static value => value.Status == "retired"));
    }

    public static MappingLineHeader Header(CriterionApplicabilityRules rules) => Header(rules.View!);

    public static MappingLineHeader Header(CriterionApplicabilityView view)
    {
        var pending = view.Versions.SingleOrDefault(static value => value.Status == "proposed");
        var active = view.Versions.SingleOrDefault(static value => value.Status == "accepted");
        return new MappingLineHeader(view.Revision, view.Versions.Count, view.DecisionId,
            pending is null ? null : new MappingLineSlot(pending.DecisionId, pending.VersionNumber, Uuid.Parse(pending.ProposedBy.Id, System.Globalization.CultureInfo.InvariantCulture)),
            active is null ? null : new MappingLineSlot(active.DecisionId, active.VersionNumber, Uuid.Parse(active.ProposedBy.Id, System.Globalization.CultureInfo.InvariantCulture)),
            view.Versions.Any(static value => value.Status == "withdrawn"));
    }

    internal static Aggregate Owner(DomainEvent frame) => frame switch
    {
        ControlCriterionMappingProposed value when value.TenantId != Uuid.Empty && value.ProgramId != Uuid.Empty &&
            value.EditionId != Uuid.Empty && !string.IsNullOrWhiteSpace(value.CriterionIdentifier) && value.ControlId != Uuid.Empty => new ControlCriterionMapping(value.TenantId, value.ProgramId, value.ControlId, value.EditionId, value.CriterionIdentifier),
        ControlCriterionMappingReviewed value when value.TenantId != Uuid.Empty && value.ProgramId != Uuid.Empty &&
            value.EditionId != Uuid.Empty && !string.IsNullOrWhiteSpace(value.CriterionIdentifier) && value.ControlId != Uuid.Empty => new ControlCriterionMapping(value.TenantId, value.ProgramId, value.ControlId, value.EditionId, value.CriterionIdentifier),
        ControlCriterionMappingRetired value when value.TenantId != Uuid.Empty && value.ProgramId != Uuid.Empty &&
            value.EditionId != Uuid.Empty && !string.IsNullOrWhiteSpace(value.CriterionIdentifier) && value.ControlId != Uuid.Empty => new ControlCriterionMapping(value.TenantId, value.ProgramId, value.ControlId, value.EditionId, value.CriterionIdentifier),
        CriterionNotApplicableProposed value when value.TenantId != Uuid.Empty && value.ProgramId != Uuid.Empty &&
            value.EditionId != Uuid.Empty && !string.IsNullOrWhiteSpace(value.CriterionIdentifier) => new CriterionApplicability(value.TenantId, value.ProgramId, value.EditionId, value.CriterionIdentifier),
        CriterionApplicabilityReviewed value when value.TenantId != Uuid.Empty && value.ProgramId != Uuid.Empty &&
            value.EditionId != Uuid.Empty && !string.IsNullOrWhiteSpace(value.CriterionIdentifier) => new CriterionApplicability(value.TenantId, value.ProgramId, value.EditionId, value.CriterionIdentifier),
        CriterionNotApplicableWithdrawn value when value.TenantId != Uuid.Empty && value.ProgramId != Uuid.Empty &&
            value.EditionId != Uuid.Empty && !string.IsNullOrWhiteSpace(value.CriterionIdentifier) => new CriterionApplicability(value.TenantId, value.ProgramId, value.EditionId, value.CriterionIdentifier),
        _ => throw new ImmutableContentIntegrityException("The native frame belongs to another line family."),
    };

    static DomainEvent Frame(object change, ImmutableContentRef content, MappingLineHeader header,
        ImmutableContentRef? firstResponse, ImmutableContentRef result) => change switch
        {
            ControlCriterionMappingProposedChange value => new ControlCriterionMappingProposed(value.TenantId, value.ProgramId, value.MappingId, value.Revision, value.VersionNumber, value.ControlId, value.ControlVersionId, value.EditionId, value.CriterionIdentifier, value.CriterionKind, value.ActorMemberId, value.ActorDisplay, value.ProposedAt, content, header, value.Operation, firstResponse, result),
            ControlCriterionMappingReviewedChange value => new ControlCriterionMappingReviewed(value.TenantId, value.ProgramId, value.MappingId, value.Revision, value.VersionNumber, value.DecisionId, value.Outcome, value.ActorMemberId, value.ActorDisplay, value.DecidedAt, value.SeparationOfDutiesWaiverId, value.ControlId, value.EditionId, value.CriterionIdentifier, content, header, value.Operation, firstResponse, result),
            ControlCriterionMappingRetiredChange value => new ControlCriterionMappingRetired(value.TenantId, value.ProgramId, value.MappingId, value.Revision, value.VersionNumber, value.ActorMemberId, value.ActorDisplay, value.RetiredAt, value.ControlId, value.EditionId, value.CriterionIdentifier, content, header, value.Operation, firstResponse, result),
            CriterionNotApplicableProposedChange value => new CriterionNotApplicableProposed(value.TenantId, value.ProgramId, value.DecisionId, value.Revision, value.VersionNumber, value.EditionId, value.CriterionIdentifier, value.ProposerMemberId, value.Actor, value.ProposedAt, content, header, value.Operation, firstResponse, result),
            CriterionApplicabilityReviewedChange value => new CriterionApplicabilityReviewed(value.TenantId, value.ProgramId, value.DecisionId, value.Revision, value.VersionNumber, value.ReviewDecisionId, value.Outcome, value.Actor, value.ReviewedAt, value.SeparationOfDutiesWaiverId, value.EditionId, value.CriterionIdentifier, content, header, value.Operation, firstResponse, result),
            CriterionNotApplicableWithdrawnChange value => new CriterionNotApplicableWithdrawn(value.TenantId, value.ProgramId, value.DecisionId, value.Revision, value.VersionNumber, value.Actor, value.WithdrawnAt, value.EditionId, value.CriterionIdentifier, content, header, value.Operation, firstResponse, result),
            _ => throw new ImmutableContentIntegrityException("The content has another business change type."),
        };

    static bool Bound(DomainEvent frame, object change) => (frame, change) switch
    {
        (ControlCriterionMappingProposed frameValue, ControlCriterionMappingProposedChange value) =>
            frameValue.TenantId == value.TenantId &&
            frameValue.ProgramId == value.ProgramId &&
            frameValue.MappingId == value.MappingId &&
            frameValue.Revision == value.Revision &&
            frameValue.VersionNumber == value.VersionNumber &&
            frameValue.ControlId == value.ControlId &&
            frameValue.ControlVersionId == value.ControlVersionId &&
            frameValue.EditionId == value.EditionId &&
            frameValue.CriterionIdentifier == value.CriterionIdentifier &&
            frameValue.CriterionKind == value.CriterionKind &&
            frameValue.ActorMemberId == value.ActorMemberId &&
            frameValue.ActorDisplay == value.ActorDisplay &&
            frameValue.ProposedAt == value.ProposedAt &&
            frameValue.Operation == value.Operation,
        (ControlCriterionMappingReviewed frameValue, ControlCriterionMappingReviewedChange value) =>
            frameValue.TenantId == value.TenantId &&
            frameValue.ProgramId == value.ProgramId &&
            frameValue.MappingId == value.MappingId &&
            frameValue.Revision == value.Revision &&
            frameValue.VersionNumber == value.VersionNumber &&
            frameValue.DecisionId == value.DecisionId &&
            frameValue.Outcome == value.Outcome &&
            frameValue.ActorMemberId == value.ActorMemberId &&
            frameValue.ActorDisplay == value.ActorDisplay &&
            frameValue.DecidedAt == value.DecidedAt &&
            frameValue.SeparationOfDutiesWaiverId == value.SeparationOfDutiesWaiverId &&
            frameValue.ControlId == value.ControlId &&
            frameValue.EditionId == value.EditionId &&
            frameValue.CriterionIdentifier == value.CriterionIdentifier &&
            frameValue.Operation == value.Operation,
        (ControlCriterionMappingRetired frameValue, ControlCriterionMappingRetiredChange value) =>
            frameValue.TenantId == value.TenantId &&
            frameValue.ProgramId == value.ProgramId &&
            frameValue.MappingId == value.MappingId &&
            frameValue.Revision == value.Revision &&
            frameValue.VersionNumber == value.VersionNumber &&
            frameValue.ActorMemberId == value.ActorMemberId &&
            frameValue.ActorDisplay == value.ActorDisplay &&
            frameValue.RetiredAt == value.RetiredAt &&
            frameValue.ControlId == value.ControlId &&
            frameValue.EditionId == value.EditionId &&
            frameValue.CriterionIdentifier == value.CriterionIdentifier &&
            frameValue.Operation == value.Operation,
        (CriterionNotApplicableProposed frameValue, CriterionNotApplicableProposedChange value) =>
            frameValue.TenantId == value.TenantId &&
            frameValue.ProgramId == value.ProgramId &&
            frameValue.DecisionId == value.DecisionId &&
            frameValue.Revision == value.Revision &&
            frameValue.VersionNumber == value.VersionNumber &&
            frameValue.EditionId == value.EditionId &&
            frameValue.CriterionIdentifier == value.CriterionIdentifier &&
            frameValue.ProposerMemberId == value.ProposerMemberId &&
            frameValue.Actor == value.Actor &&
            frameValue.ProposedAt == value.ProposedAt &&
            frameValue.Operation == value.Operation,
        (CriterionApplicabilityReviewed frameValue, CriterionApplicabilityReviewedChange value) =>
            frameValue.TenantId == value.TenantId &&
            frameValue.ProgramId == value.ProgramId &&
            frameValue.DecisionId == value.DecisionId &&
            frameValue.Revision == value.Revision &&
            frameValue.VersionNumber == value.VersionNumber &&
            frameValue.ReviewDecisionId == value.ReviewDecisionId &&
            frameValue.Outcome == value.Outcome &&
            frameValue.Actor == value.Actor &&
            frameValue.ReviewedAt == value.ReviewedAt &&
            frameValue.SeparationOfDutiesWaiverId == value.SeparationOfDutiesWaiverId &&
            frameValue.EditionId == value.EditionId &&
            frameValue.CriterionIdentifier == value.CriterionIdentifier &&
            frameValue.Operation == value.Operation,
        (CriterionNotApplicableWithdrawn frameValue, CriterionNotApplicableWithdrawnChange value) =>
            frameValue.TenantId == value.TenantId &&
            frameValue.ProgramId == value.ProgramId &&
            frameValue.DecisionId == value.DecisionId &&
            frameValue.Revision == value.Revision &&
            frameValue.VersionNumber == value.VersionNumber &&
            frameValue.Actor == value.Actor &&
            frameValue.WithdrawnAt == value.WithdrawnAt &&
            frameValue.EditionId == value.EditionId &&
            frameValue.CriterionIdentifier == value.CriterionIdentifier &&
            frameValue.Operation == value.Operation,
        _ => false,
    };

    static void ValidateAuthored(JsonElement authored, object change)
    {
        var valid = change switch
        {
            ControlCriterionMappingProposedChange value => ValidProposeControlCriterionMapping(authored, value),
            ControlCriterionMappingReviewedChange value => ValidReviewControlCriterionMapping(authored, value),
            ControlCriterionMappingRetiredChange value => ValidRetireControlCriterionMapping(authored, value),
            CriterionNotApplicableProposedChange value => ValidProposeCriterionNotApplicable(authored, value),
            CriterionApplicabilityReviewedChange value => ValidReviewCriterionApplicability(authored, value),
            CriterionNotApplicableWithdrawnChange value => ValidWithdrawCriterionNotApplicable(authored, value),
            _ => false,
        };
        if (!valid)
            throw new ImmutableContentIntegrityException("The immutable authored intent does not match its native acceptance and business change.");
    }
    static bool ValidProposeControlCriterionMapping(JsonElement authored, ControlCriterionMappingProposedChange value)
    {
        var request = authored.Deserialize(ComplianceCoreJsonContext.Default.ProposeControlCriterionMapping);
        if (request is null || value.Operation is null || value.Operation.OriginalExpectedRevision is null ||
            AuthoredIntentDigest.Compute(request) != value.Operation.AuthoredIntentSha256 ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000)
            return false;
        if (string.IsNullOrWhiteSpace(request.ApplicabilityExplanation) || request.ApplicabilityExplanation.Trim().Length > 4000)
            return false;
        var expected = new ProposeControlCriterionMapping(value.MappingId, value.TenantId, value.ProgramId, value.ControlId, value.ControlVersionId, value.EditionId, value.CriterionIdentifier, (long)value.Operation.OriginalExpectedRevision!, value.Rationale, value.ApplicabilityExplanation);
        return request with { Rationale = request.Rationale.Trim(), ApplicabilityExplanation = request.ApplicabilityExplanation.Trim() } == expected;
    }

    static bool ValidReviewControlCriterionMapping(JsonElement authored, ControlCriterionMappingReviewedChange value)
    {
        var request = authored.Deserialize(ComplianceCoreJsonContext.Default.ReviewControlCriterionMapping);
        if (request is null || value.Operation is null || value.Operation.OriginalExpectedRevision is null ||
            AuthoredIntentDigest.Compute(request) != value.Operation.AuthoredIntentSha256 ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000)
            return false;
        var expected = new ReviewControlCriterionMapping(value.DecisionId, value.TenantId, value.ProgramId, value.MappingId, (long)value.Operation.OriginalExpectedRevision!, value.Outcome, value.Rationale, value.SeparationOfDutiesWaiverId);
        return request with { Rationale = request.Rationale.Trim() } == expected;
    }

    static bool ValidRetireControlCriterionMapping(JsonElement authored, ControlCriterionMappingRetiredChange value)
    {
        var request = authored.Deserialize(ComplianceCoreJsonContext.Default.RetireControlCriterionMapping);
        if (request is null || value.Operation is null || value.Operation.OriginalExpectedRevision is null ||
            AuthoredIntentDigest.Compute(request) != value.Operation.AuthoredIntentSha256 ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000)
            return false;
        var expected = new RetireControlCriterionMapping(value.Operation.LogicalId, value.TenantId, value.ProgramId, value.MappingId, (long)value.Operation.OriginalExpectedRevision!, value.Rationale);
        return request with { Rationale = request.Rationale.Trim() } == expected;
    }

    static bool ValidProposeCriterionNotApplicable(JsonElement authored, CriterionNotApplicableProposedChange value)
    {
        var request = authored.Deserialize(ComplianceCoreJsonContext.Default.ProposeCriterionNotApplicable);
        if (request is null || value.Operation is null || value.Operation.OriginalExpectedRevision is null ||
            AuthoredIntentDigest.Compute(request) != value.Operation.AuthoredIntentSha256 ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000)
            return false;
        var expected = new ProposeCriterionNotApplicable(value.DecisionId, value.TenantId, value.ProgramId, value.EditionId, value.CriterionIdentifier, (long)value.Operation.OriginalExpectedRevision!, value.Rationale);
        return request with { Rationale = request.Rationale.Trim() } == expected;
    }

    static bool ValidReviewCriterionApplicability(JsonElement authored, CriterionApplicabilityReviewedChange value)
    {
        var request = authored.Deserialize(ComplianceCoreJsonContext.Default.ReviewCriterionApplicability);
        if (request is null || value.Operation is null || value.Operation.OriginalExpectedRevision is null ||
            AuthoredIntentDigest.Compute(request) != value.Operation.AuthoredIntentSha256 ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000)
            return false;
        var expected = new ReviewCriterionApplicability(value.ReviewDecisionId, value.TenantId, value.ProgramId, value.DecisionId, (long)value.Operation.OriginalExpectedRevision!, value.Outcome, value.Rationale, value.SeparationOfDutiesWaiverId);
        return request with { Rationale = request.Rationale.Trim() } == expected;
    }

    static bool ValidWithdrawCriterionNotApplicable(JsonElement authored, CriterionNotApplicableWithdrawnChange value)
    {
        var request = authored.Deserialize(ComplianceCoreJsonContext.Default.WithdrawCriterionNotApplicable);
        if (request is null || value.Operation is null || value.Operation.OriginalExpectedRevision is null ||
            AuthoredIntentDigest.Compute(request) != value.Operation.AuthoredIntentSha256 ||
            string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 4000)
            return false;
        var expected = new WithdrawCriterionNotApplicable(value.Operation.LogicalId, value.TenantId, value.ProgramId, value.DecisionId, (long)value.Operation.OriginalExpectedRevision!, value.Rationale);
        return request with { Rationale = request.Rationale.Trim() } == expected;
    }

}
