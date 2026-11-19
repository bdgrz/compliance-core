using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateDestinationCommandsTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();

    [Fact]
    public void ShouldMapExactControlPayloadGivenTypedControlEntry()
    {
        // Arrange
        var payload = new MethodologyTemplateControlContent(" ctrl-01 ", ControlContent());
        var entry = Entry("control") with { Control = payload };

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<CreateControlDraft>(result.Value.Request);
        Assert.Equal(ControlDraft.IdFor(Tenant, Program, payload.Identifier), result.Value.RecordId);
        Assert.Equal("CTRL-01", command.Identifier);
        Assert.Equal(Tenant, command.TenantId);
        Assert.Equal(Program, command.ProgramId);
        Assert.Equal(payload.Content.Title, command.Content.Title);
        Assert.Equal(payload.Content.Objective, command.Content.Objective);
        Assert.Equal(payload.Content.ExpectedEvidenceDescriptions, command.Content.ExpectedEvidenceDescriptions);
    }

    [Fact]
    public void ShouldMapExactPolicyPayloadGivenTypedPolicyEntry()
    {
        // Arrange
        var payload = new MethodologyTemplatePolicyContent(" pol-01 ", PolicyContent());
        var entry = Entry("policy") with { Policy = payload };

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<CreatePolicyDraft>(result.Value.Request);
        Assert.Equal(Policy.IdFor(Tenant, Program, payload.Identifier), command.PolicyId);
        Assert.Equal(command.PolicyId, result.Value.RecordId);
        Assert.Equal("POL-01", command.Identifier);
        Assert.Equal(payload.Content, command.Content);
    }

    [Fact]
    public void ShouldMapExactRiskPayloadGivenTypedRiskEntry()
    {
        // Arrange
        var payload = new MethodologyTemplateRiskContent(" risk-01 ", "Risk title", "Risk scenario",
            "Risk effect", "Firm-authored source note");
        var entry = Entry("risk") with { Risk = payload };

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<CreateRiskDraft>(result.Value.Request);
        Assert.Equal(RiskDraft.IdFor(Tenant, Program, payload.Identifier), command.RiskId);
        Assert.Equal(command.RiskId, result.Value.RecordId);
        Assert.Equal("RISK-01", command.Identifier);
        Assert.Equal(payload.Title, command.Title);
        Assert.Equal(payload.Scenario, command.Scenario);
        Assert.Equal(payload.PotentialEffect, command.PotentialEffect);
        Assert.Equal(payload.SourceNote, command.SourceNote);
    }

    [Theory]
    [InlineData("control")]
    [InlineData("policy")]
    [InlineData("risk")]
    public void ShouldRefuseMissingPayloadGivenGenericSummaryOnly(string kind)
    {
        // Arrange
        var entry = Entry(kind);

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal("destination_payload_missing", result.Error?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseInconsistentPayloadGivenAmbiguousOrWrongKind(bool ambiguous)
    {
        // Arrange
        var entry = Entry("control") with
        {
            Control = ambiguous ? new("CTRL-01", ControlContent()) : null,
            Policy = new("POL-01", PolicyContent()),
        };

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal("destination_payload_inconsistent", result.Error?.Message);
    }

    [Fact]
    public void ShouldProposeControlWithoutOverwriteGivenExactApprovedVersion()
    {
        // Arrange
        var entry = Entry("control") with { Control = new("CTRL-01", ControlContent()) };
        var commandId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var recordId = ControlDraft.IdFor(Tenant, Program, "CTRL-01");

        // Act
        var result = MethodologyTemplateDestinationCommands.Propose(commandId, Tenant, Program, entry,
            recordId, expectedControlVersionId: versionId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<ProposeControlSuccessor>(result.Value.Request);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(recordId, command.ControlId);
        Assert.Equal(versionId, command.ExpectedApprovedVersionId);
        Assert.Equal(entry.Control!.Content.Title, command.Content.Title);
    }

    [Fact]
    public void ShouldProposePolicyWithoutOverwriteGivenExactApprovedVersion()
    {
        // Arrange
        var entry = Entry("policy") with { Policy = new("POL-01", PolicyContent()) };
        var commandId = Uuid.CreateVersion4();
        var recordId = Policy.IdFor(Tenant, Program, "POL-01");

        // Act
        var result = MethodologyTemplateDestinationCommands.Propose(commandId, Tenant, Program, entry,
            recordId, expectedPolicyVersion: 3);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<ProposePolicySuccessor>(result.Value.Request);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(recordId, command.PolicyId);
        Assert.Equal(3, command.ExpectedVersion);
        Assert.Equal(entry.Policy!.Content, command.Content);
    }

    [Fact]
    public void ShouldRefuseProposalGivenDestinationIdentityOrBaseMismatch()
    {
        // Arrange
        var entry = Entry("control") with { Control = new("CTRL-01", ControlContent()) };
        var correctId = ControlDraft.IdFor(Tenant, Program, "CTRL-01");

        // Act
        var wrongId = MethodologyTemplateDestinationCommands.Propose(Uuid.CreateVersion4(), Tenant, Program,
            entry, Uuid.CreateVersion4(), expectedControlVersionId: Uuid.CreateVersion4());
        var missingBase = MethodologyTemplateDestinationCommands.Propose(Uuid.CreateVersion4(), Tenant, Program,
            entry, correctId);
        var wrongBaseKind = MethodologyTemplateDestinationCommands.Propose(Uuid.CreateVersion4(), Tenant, Program,
            entry, correctId, expectedControlVersionId: Uuid.CreateVersion4(), expectedPolicyVersion: 1);

        // Assert
        Assert.Equal("destination_identity_mismatch", wrongId.Error?.Message);
        Assert.Equal("destination_base_missing_or_inconsistent", missingBase.Error?.Message);
        Assert.Equal("destination_base_missing_or_inconsistent", wrongBaseKind.Error?.Message);
    }

    [Fact]
    public void ShouldRefuseRiskProposalGivenNoGovernedProposalReceiver()
    {
        // Arrange
        var entry = Entry("risk") with { Risk = new("RISK-01", "Risk", "Scenario", "Effect") };

        // Act
        var result = MethodologyTemplateDestinationCommands.Propose(Uuid.CreateVersion4(), Tenant, Program,
            entry, RiskDraft.IdFor(Tenant, Program, "RISK-01"), expectedRiskRevision: 2);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal("risk_revision_proposal_unavailable", result.Error?.Message);
    }

    [Fact]
    public void ShouldRetainTemplateProvenanceGivenVerifiedSourceIdentity()
    {
        // Arrange
        var template = Uuid.CreateVersion4();
        var entry = Entry("control") with { Control = new("CTRL-01", ControlContent()) };

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry,
            templateId: template, templateVersion: 2);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<CreateControlDraft>(result.Value.Request);
        Assert.Equal("template", command.Content.Provenance?.Origin);
        Assert.Equal($"methodology:{template}:{entry.EntryKey}", command.Content.Provenance?.SourceReference);
        Assert.Equal("2", command.Content.Provenance?.SourceVersion);
    }

    [Fact]
    public void ShouldRefuseIdentifierReuseGivenSamePolicyContentFromDifferentTemplate()
    {
        // Arrange
        var firstTemplate = Uuid.CreateVersion4();
        var secondTemplate = Uuid.CreateVersion4();
        var entry = Entry("policy") with { Policy = new("POL-01", PolicyContent()) };
        var first = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry,
            templateId: firstTemplate, templateVersion: 1);
        var second = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry,
            templateId: secondTemplate, templateVersion: 1);
        var firstCommand = Assert.IsType<CreatePolicyDraft>(first.Value.Request);
        var secondCommand = Assert.IsType<CreatePolicyDraft>(second.Value.Request);
        var owner = new PolicyRules(Tenant, firstCommand.PolicyId);
        var member = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(member, "Compliance Lead");
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        Assert.True(owner.Create(Program, firstCommand.PolicyId, firstCommand.Identifier, firstCommand.Content,
            actor, member, now).IsSuccess);
        owner.ClearPendingChanges();

        // Act
        var retry = owner.Create(Program, firstCommand.PolicyId, firstCommand.Identifier, firstCommand.Content,
            actor, member, now.AddMinutes(1));
        var reused = owner.Create(Program, secondCommand.PolicyId, secondCommand.Identifier, secondCommand.Content,
            actor, member, now.AddMinutes(2));

        // Assert
        Assert.Equal(firstCommand.PolicyId, secondCommand.PolicyId);
        Assert.NotEqual(PolicyRules.Hash(firstCommand.Content), PolicyRules.Hash(secondCommand.Content));
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, reused.Error?.Kind);
        Assert.Equal(new MethodologyTemplateEntryProvenance(firstTemplate, 1, entry.EntryKey),
            owner.DraftContent?.TemplateOrigin);
        Assert.Empty(owner.PendingChanges);
    }

    [Fact]
    public void ShouldReplaceUntrustedPolicyOriginGivenCurrentVerifiedTemplateSource()
    {
        // Arrange
        var template = Uuid.CreateVersion4();
        var staleOrigin = new MethodologyTemplateEntryProvenance(Uuid.CreateVersion4(), 10, "another-entry");
        var entry = Entry("policy") with
        { Policy = new("POL-01", PolicyContent() with { TemplateOrigin = staleOrigin }) };

        // Act
        var result = MethodologyTemplateDestinationCommands.Create(Tenant, Program, entry,
            templateId: template, templateVersion: 2);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var command = Assert.IsType<CreatePolicyDraft>(result.Value.Request);
        Assert.Equal(new MethodologyTemplateEntryProvenance(template, 2, entry.EntryKey), command.Content.TemplateOrigin);
    }

    [Fact]
    public void ShouldFreezePayloadCollectionsAndChangeDigestGivenTypedContentRevision()
    {
        // Arrange
        var evidence = new List<string> { "Evidence specification" };
        var entry = Entry("control") with
        { Control = new("CTRL-01", ControlContent() with { ExpectedEvidenceDescriptions = evidence }) };
        var before = ContentDigest.OfJson(entry);

        // Act
        var frozen = MethodologyTemplateDestinationCommands.Freeze(entry);
        evidence[0] = "Mutated evidence specification";
        var revised = frozen with { Control = frozen.Control! with
        { Content = frozen.Control!.Content with { Objective = "A different objective" } } };

        // Assert
        Assert.Equal("Evidence specification", Assert.Single(frozen.Control!.Content.ExpectedEvidenceDescriptions));
        Assert.Equal(before, ContentDigest.OfJson(frozen));
        Assert.NotEqual(before, ContentDigest.OfJson(revised));
    }

    static MethodologyTemplateRecordContent Entry(string kind) => new("entry-01", kind,
        "Display title only", "Display summary only", []);
    static ControlDraftContent ControlContent() => new("Control title", "Control objective", "Control description",
        "Control implementation", ["Expected control evidence"]);
    static PolicyContent PolicyContent() => new("Policy title", "Policy purpose", "core_security", null,
        12, "Policy body", null, null, null);
}
