using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Maps exact authored entries to destination commands without synthesizing business content.</summary>
public static class MethodologyTemplateDestinationCommands
{
    public static Result<MethodologyTemplateDestinationCommand> Create(Uuid tenantId, Uuid programId,
        MethodologyTemplateRecordContent entry, Uuid templateId = default, long templateVersion = 0)
    {
        if (Scope(tenantId, programId, templateId, templateVersion) is { } scopeError)
            return Failure(scopeError);
        if (ValidatePayload(entry) is { } payloadError)
            return Result<MethodologyTemplateDestinationCommand>.Failure(payloadError);
        var frozen = Freeze(entry);
        return frozen.RecordKind switch
        {
            "control" => Success(ControlDraft.IdFor(tenantId, programId, frozen.Control!.Identifier), "control",
                new CreateControlDraft(tenantId, programId, ControlDraft.NormalizeIdentifier(frozen.Control!.Identifier),
                    ControlContent(frozen, templateId, templateVersion))),
            "policy" => Success(Policy.IdFor(tenantId, programId, frozen.Policy!.Identifier), "policy",
                new CreatePolicyDraft(Policy.IdFor(tenantId, programId, frozen.Policy!.Identifier), tenantId, programId,
                    Policy.NormalizeIdentifier(frozen.Policy!.Identifier), PolicyContent(frozen, templateId, templateVersion))),
            "risk" => Success(RiskDraft.IdFor(tenantId, programId, frozen.Risk!.Identifier), "risk",
                new CreateRiskDraft(RiskDraft.IdFor(tenantId, programId, frozen.Risk!.Identifier), tenantId, programId,
                    RiskDraft.NormalizeIdentifier(frozen.Risk!.Identifier), frozen.Risk!.Title, frozen.Risk!.Scenario,
                    frozen.Risk!.PotentialEffect, frozen.Risk!.SourceNote)),
            _ => Failure("destination_kind_unsupported"),
        };
    }

    public static Result<MethodologyTemplateDestinationCommand> Propose(Uuid commandId, Uuid tenantId,
        Uuid programId, MethodologyTemplateRecordContent entry, Uuid recordId,
        Uuid? expectedControlVersionId = null, long? expectedPolicyVersion = null, long? expectedRiskRevision = null,
        Uuid templateId = default, long templateVersion = 0)
    {
        if (commandId == Uuid.Empty)
            return Failure("destination_command_id_missing");
        var creation = Create(tenantId, programId, entry, templateId, templateVersion);
        if (!creation.IsSuccess)
            return creation;
        if (recordId == Uuid.Empty || creation.Value.RecordId != recordId)
            return Failure("destination_identity_mismatch");
        return creation.Value.Request switch
        {
            CreateControlDraft control when expectedControlVersionId is { } version && version != Uuid.Empty &&
                expectedPolicyVersion is null && expectedRiskRevision is null => Success(recordId, "control",
                    new ProposeControlSuccessor(commandId, tenantId, programId, recordId, version, control.Content)),
            CreatePolicyDraft policy when expectedPolicyVersion is > 0 && expectedControlVersionId is null &&
                expectedRiskRevision is null => Success(recordId, "policy",
                    new ProposePolicySuccessor(commandId, tenantId, programId, recordId, expectedPolicyVersion.Value,
                        policy.Content)),
            CreateRiskDraft => Failure("risk_revision_proposal_unavailable"),
            _ => Failure("destination_base_missing_or_inconsistent"),
        };
    }

    /// <summary>Checks payload structure using the destination's own content rules. Legacy decoding may permit absent payloads.</summary>
    public static RequestError? ValidatePayload(MethodologyTemplateRecordContent? entry, bool requirePayload = true)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.EntryKey) || entry.EntryKey.Length > 100 ||
            string.IsNullOrWhiteSpace(entry.Title) || entry.Title.Length > 200 ||
            entry.CriteriaReferences is null || entry.ContributionApprovalId is not null)
            return Error("destination_entry_invalid");
        if (entry.RecordKind is not ("control" or "policy" or "risk"))
            return Error("destination_kind_unsupported");
        var count = (entry.Control is not null ? 1 : 0) + (entry.Policy is not null ? 1 : 0) +
            (entry.Risk is not null ? 1 : 0);
        if (count == 0)
            return requirePayload ? Error("destination_payload_missing") : null;
        if (count != 1 || entry.RecordKind == "control" && entry.Control is null ||
            entry.RecordKind == "policy" && entry.Policy is null || entry.RecordKind == "risk" && entry.Risk is null)
            return Error("destination_payload_inconsistent");
        if (entry.Control is { } control)
            return ControlDraft.ValidateCreateInput(control.Identifier, control.Content) is { } controlError
                ? Error($"destination_payload_invalid: {controlError.Message}") : null;
        if (entry.Policy is { } policy)
            return (PolicyRules.ValidateIdentifier(Policy.NormalizeIdentifier(policy.Identifier)) ??
                PolicyRules.ValidateContent(policy.Content)) is { } policyError
                ? Error($"destination_payload_invalid: {policyError}") : null;
        var risk = entry.Risk!;
        return RiskDraftRules.Validate(RiskDraft.NormalizeIdentifier(risk.Identifier),
            new RiskDraftContent(risk.Title, risk.Scenario, risk.PotentialEffect, risk.SourceNote)) is { } riskError
            ? Error($"destination_payload_invalid: {riskError.Message}") : null;
    }

    /// <summary>Owns every collection in the reusable payload, retaining null values and the exact authored semantics.</summary>
    public static MethodologyTemplateRecordContent Freeze(MethodologyTemplateRecordContent entry) => entry with
    {
        CriteriaReferences = Array.AsReadOnly(entry.CriteriaReferences.ToArray()),
        Control = entry.Control is { } control ? control with
        {
            Content = control.Content with
            {
                ExpectedEvidenceDescriptions = Array.AsReadOnly(control.Content.ExpectedEvidenceDescriptions.ToArray()),
                Applicability = control.Content.Applicability is { } controlApplicability
                    ? Array.AsReadOnly(controlApplicability.ToArray()) : null,
            },
        } : null,
        Policy = entry.Policy is { } policy ? policy with
        {
            Content = policy.Content with
            {
                AudienceTeams = policy.Content.AudienceTeams is { } teams ? Array.AsReadOnly(teams.ToArray()) : null,
                Applicability = policy.Content.Applicability is { } policyApplicability
                    ? Array.AsReadOnly(policyApplicability.ToArray()) : null,
            },
        } : null,
    };

    static ControlDraftContent ControlContent(MethodologyTemplateRecordContent entry, Uuid templateId, long version) =>
        templateId == Uuid.Empty ? entry.Control!.Content : entry.Control!.Content with
        {
            Provenance = new ControlContentProvenance("template", entry.Title,
                $"methodology:{templateId}:{entry.EntryKey}", version.ToString(CultureInfo.InvariantCulture)),
        };

    static Bdgrz.Compliance.Features.Policies.Models.PolicyContent PolicyContent(
        MethodologyTemplateRecordContent entry, Uuid templateId, long version) =>
        templateId == Uuid.Empty ? entry.Policy!.Content : entry.Policy!.Content with
        {
            TemplateOrigin = new MethodologyTemplateEntryProvenance(templateId, version, entry.EntryKey),
        };

    static string? Scope(Uuid tenantId, Uuid programId, Uuid templateId, long templateVersion) =>
        tenantId == Uuid.Empty || programId == Uuid.Empty ? "destination_scope_missing" :
        templateId == Uuid.Empty && templateVersion != 0 || templateId != Uuid.Empty && templateVersion < 1
            ? "destination_source_inconsistent" : null;
    static RequestError Error(string reason) => new(RequestErrorKind.Validation, reason);
    static Result<MethodologyTemplateDestinationCommand> Failure(string reason) =>
        Result<MethodologyTemplateDestinationCommand>.Failure(Error(reason));
    static Result<MethodologyTemplateDestinationCommand> Success(Uuid id, string kind, IRequestBase request) =>
        Result<MethodologyTemplateDestinationCommand>.Success(new MethodologyTemplateDestinationCommand(id, kind, request));
}
