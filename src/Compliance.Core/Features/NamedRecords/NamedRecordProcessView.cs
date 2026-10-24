using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>
///     What a name-change process step needs to build its receiver command: the frozen intent, the accepted root fact
///     and the claim the step targets. The claim targets of the process record carry the stream resource, the name kind
///     and the normalized name (<see cref="EncodeTarget" />), frozen when the process started, so no step ever reads
///     the record's history to learn a name. Which receivers apply is decided here for all three families.
/// </summary>
public sealed class NamedRecordProcessView
{
    NamedRecordProcessView(NamedRecordKindDescriptor kind, ProcessStep step, IServiceProvider services,
        RecordNameChangeIntent intent, INamedRecordFact root)
    {
        Kind = kind;
        Step = step;
        Services = services;
        Intent = intent;
        Root = root;
    }

    /// <summary>The family being changed.</summary>
    public NamedRecordKindDescriptor Kind { get; }

    /// <summary>The step being dispatched.</summary>
    public ProcessStep Step { get; }

    /// <summary>The scoped services of the driver.</summary>
    public IServiceProvider Services { get; }

    /// <summary>The frozen intent of the change.</summary>
    public RecordNameChangeIntent Intent { get; }

    /// <summary>The accepted change fact that started the process.</summary>
    public INamedRecordFact Root { get; }

    /// <summary>The tenant realm.</summary>
    public string Realm => Step.Record.Key.Realm;

    /// <summary>The generation of the accepted change.</summary>
    public long Generation => Root.Generation;

    /// <summary>The revision the change expects of the record.</summary>
    public long ExpectedRevision => (long)Intent.ExpectedRecordRevision;

    /// <summary>True once an abort fenced the operation and only release stages still run.</summary>
    public bool Aborting => Step.Record.State == ProcessState.Aborting;

    /// <summary>The content store.</summary>
    public IImmutableContentStore Content => Services.GetRequiredService<IImmutableContentStore>();

    /// <summary>The source fact resolver.</summary>
    public ISourceFactResolver Resolver => Services.GetRequiredService<ISourceFactResolver>();

    /// <summary>The fact a receiver of this record committed, if any.</summary>
    public NativeFactReference? FactOf(string receiver) =>
        Step.Record.Facts.TryGetValue(receiver, out var fact) ? fact : null;

    /// <summary>The per-target fact that acquired the new claim, rather than a different name held by the same change.</summary>
    public NativeFactReference? NewClaimFact(string receiver)
    {
        if (Intent.NewClaimKey is not { } desired)
            return null;
        var encoded = Step.Record.Targets.SingleOrDefault(target =>
            new EventStreamAddress(Realm, Kind.ClaimArea, DecodeTarget(target).Resource) == desired);
        return encoded is null ? null : Step.Record.Facts.GetValueOrDefault(ProcessRecord.FactKey(receiver, encoded));
    }

    /// <summary>The receiver fields every command of this step shares for <paramref name="target" />.</summary>
    public NameChangeStep StepFor(EventStreamAddress target) => new(Step.CommandId, target,
        Root.Operation.RootOperationId, Generation, ExpectedRevision, Step.Record.Intent, Intent.RecordId);

    /// <summary>The claim the step targets, or null when the step has no target (a change that claims nothing).</summary>
    public ClaimTarget? Target()
    {
        if (Step.Target is not { } encoded)
            return null;
        var (resource, nameKind, normalized) = DecodeTarget(encoded);
        return new ClaimTarget(new EventStreamAddress(Realm, Kind.ClaimArea, resource), nameKind, normalized);
    }

    /// <summary>Opens the view of a step; the intent is read from the content store and the root from the broker.</summary>
    public static async ValueTask<NamedRecordProcessView> OpenAsync(NamedRecordKindDescriptor kind, ProcessStep step,
        IServiceProvider services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(services);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<RecordNameChangeIntent>(step.Record.Key.Realm, step.Record.Intent, ct)
            .ConfigureAwait(false);
        var resolved = await services.GetRequiredService<ISourceFactResolver>()
            .ResolveAsync(step.Record.Source.Stream, step.Record.Source.AggregateVersion - 1, ct).ConfigureAwait(false);
        var root = resolved.Event as INamedRecordFact
            ?? throw new SourceFactVerificationException("The process root is not a record change fact.");
        return new NamedRecordProcessView(kind, step, services, intent, root);
    }

    /// <summary>Encodes a claim target of the process record: stream resource, name kind and normalized name.</summary>
    public static string EncodeTarget(EventStreamAddress key, string nameKind, string normalizedName)
    {
        ArgumentNullException.ThrowIfNull(key);
        return $"{key.Resource}:{nameKind}:{normalizedName}";
    }

    /// <summary>Decodes a claim target of the process record.</summary>
    public static (string Resource, string NameKind, string NormalizedName) DecodeTarget(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var first = target.IndexOf(':', StringComparison.Ordinal);
        var second = first < 0 ? -1 : target.IndexOf(':', first + 1);
        if (second < 0)
            throw new InvalidOperationException("A name-change claim target is malformed.");
        return (target[..first], target[(first + 1)..second], target[(second + 1)..]);
    }
}

/// <summary>One claim a process step targets: its stream, name kind and normalized name.</summary>
public sealed record ClaimTarget(EventStreamAddress Stream, string NameKind, string NormalizedName);
