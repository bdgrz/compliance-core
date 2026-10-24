using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds committed handoff and feedback thread facts directly through the owners for tests that exercise readers,
///     projections and authorizers rather than the commands: the selection is written as verified content and every
///     fact carries its native provenance with the revision it was accepted at.
/// </summary>
static class AdvisorSeed
{
    static readonly ImmutableContentRef FakeContent = AdvisorEvents.Ref();

    /// <summary>Creates a handoff and, unless it stays a draft, opens it for review.</summary>
    public static async Task HandoffAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        IReadOnlyList<AdvisorRecordVersion> selection, ActorReference lead, DateTimeOffset createdAt,
        DateOnly expiresOn, bool ready = true, string purpose = "Review", Uuid? comparedTo = null)
    {
        var manifest = await WriteSelectionAsync(provider, tenantId, selection);
        var stream = new AdvisorHandoff(tenantId, programId, handoffId).Stream;
        await ProgramManagementServices.SeedAsync(provider, new AdvisorHandoff(tenantId, programId, handoffId),
            handoff => handoff.Create(AdvisorEvents.Provenance(handoffId, stream, lead, createdAt), manifest, purpose,
                expiresOn, lead, createdAt, comparedTo, FakeContent, FakeContent));
        if (ready)
            await ReadyAsync(provider, tenantId, programId, handoffId, lead, createdAt.AddMinutes(1));
    }

    /// <summary>Opens the draft handoff for review at its current revision.</summary>
    public static async Task ReadyAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        ActorReference lead, DateTimeOffset at)
    {
        var current = await ProgramManagementServices.HydrateAsync(provider,
            new AdvisorHandoff(tenantId, programId, handoffId));
        var operation = AdvisorEvents.Provenance(Uuid.CreateVersion4(), current.Stream, lead, at, (ulong)current.Revision);
        await ProgramManagementServices.SeedAsync(provider, new AdvisorHandoff(tenantId, programId, handoffId),
            handoff => handoff.MarkReady(operation, lead, at, FakeContent, FakeContent));
    }

    /// <summary>Closes or revokes the open handoff at its current revision.</summary>
    public static async Task EndAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        ActorReference lead, DateTimeOffset at, bool revoke = false)
    {
        var current = await ProgramManagementServices.HydrateAsync(provider,
            new AdvisorHandoff(tenantId, programId, handoffId));
        var operation = AdvisorEvents.Provenance(Uuid.CreateVersion4(), current.Stream, lead, at, (ulong)current.Revision);
        await ProgramManagementServices.SeedAsync(provider, new AdvisorHandoff(tenantId, programId, handoffId),
            handoff => revoke
                ? handoff.Revoke(operation, "Access removed", lead, at, FakeContent, FakeContent)
                : handoff.Close(operation, "Review complete", lead, at, FakeContent, FakeContent));
    }

    /// <summary>Admits feedback on the ready handoff and materializes its thread at revision one.</summary>
    public static async Task FeedbackAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        Uuid feedbackId, AdvisorRecordVersion source, string kind, string body, ActorReference author,
        DateTimeOffset at)
    {
        var current = await ProgramManagementServices.HydrateAsync(provider,
            new AdvisorHandoff(tenantId, programId, handoffId));
        var operation = AdvisorEvents.Provenance(feedbackId, current.Stream, author, at, (ulong)current.Revision);
        await ProgramManagementServices.SeedAsync(provider, new AdvisorHandoff(tenantId, programId, handoffId),
            handoff => handoff.AdmitFeedback(operation, FakeContent, DateOnly.FromDateTime(at.UtcDateTime),
                FakeContent));
        var thread = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var materialization = AdvisorEvents.Provenance(Uuid.CreateVersion4(), thread.Stream, author, at);
        var authored = new RecordAdvisorFeedback(tenantId, programId, handoffId, feedbackId, source.RecordId,
            source.VersionId, kind, body, (long)current.Revision)
        {
            SelectedSource = source.IsSupported ? source : null,
        };
        await ProgramManagementServices.SeedAsync(provider, thread,
            item => item.Materialize(materialization, authored, author, at, FakeContent));
    }

    static async Task<ImmutableContentRef> WriteSelectionAsync(IServiceProvider provider, Uuid tenantId,
        IReadOnlyList<AdvisorRecordVersion> selection)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IImmutableContentStore>().WriteJsonAsync(
            tenantId.ToString(), Uuid.CreateVersion4(), "AdvisorHandoffCreated.RecordVersionsManifest.v1", selection);
    }
}
