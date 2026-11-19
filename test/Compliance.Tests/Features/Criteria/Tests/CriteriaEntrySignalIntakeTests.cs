using System.Security.Cryptography;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEntrySignalIntakeTests
{
    [Fact]
    public async Task ShouldScheduleOtherProposalGivenAnUnresolvedEntry()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var first = Proposal(edition, "CC6.1");
        var second = Proposal(edition, "CC6.2");
        var records = new MemoryRecords();
        var intake = new CriteriaEntrySignalIntake(new MemoryContent(), records, TimeProvider.System);

        // Act
        var one = await intake.AcceptAsync(new CriteriaEntry(edition, "CC6.1").Stream, 0, first, CancellationToken.None);
        var two = await intake.AcceptAsync(new CriteriaEntry(edition, "CC6.2").Stream, 0, second, CancellationToken.None);

        // Assert
        Assert.True(one.IsSuccess);
        Assert.True(two.IsSuccess);
        Assert.Equal(2, records.Items.Count);
        Assert.All(records.Items.Values, item => Assert.Single(item.Targets));
    }

    [Fact]
    public async Task ShouldRecoverOneSignalGivenAnUnknownCreateResult()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var proposal = Proposal(edition, "CC6.1");
        var stream = new CriteriaEntry(edition, "CC6.1").Stream;
        var records = new MemoryRecords { LoseFirstAcknowledgement = true };
        var intake = new CriteriaEntrySignalIntake(new MemoryContent(), records, TimeProvider.System);

        // Act
        var lost = await Record.ExceptionAsync(async () => await intake.AcceptAsync(stream, 0, proposal, CancellationToken.None));
        var retried = await intake.AcceptAsync(stream, 0, proposal, CancellationToken.None);

        // Assert
        Assert.IsType<TimeoutException>(lost);
        Assert.True(retried.IsSuccess);
        Assert.Single(records.Items);
    }

    static CriterionChangeProposed Proposal(Uuid edition, string identifier)
    {
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, identifier);
        var intent = new CriterionChangeIntent(edition, Uuid.CreateVersion4(), identifier, operation,
            0, "create", "criterion", "security", null, null, "summary");
        var actor = new ActorAttribution("operator", "bdgrz");
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
        var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, operation,
            owner.Stream.ToString(), new string('a', 64), DateTimeOffset.UtcNow, "policy", source, source)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = source
        };
        var value = new CriterionChangeProposed(intent, 1, CriteriaEntry.IncarnationOf(edition, identifier, operation),
            actor, admission, null, Uuid.CreateVersion4());
        value.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, DateTimeOffset.UtcNow));
        return value;
    }

    sealed class MemoryRecords : IProcessRecordStore
    {
        public Dictionary<ProcessKey, ProcessRecord> Items { get; } = [];
        public bool LoseFirstAcknowledgement { get; set; }
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
            Task.FromResult(Items.GetValueOrDefault(key));
        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default)
        {
            if (!Items.TryGetValue(record.Key, out var stored))
            {
                stored = record with { Version = 1 };
                Items.Add(record.Key, stored);
                if (LoseFirstAcknowledgement)
                {
                    LoseFirstAcknowledgement = false;
                    throw new TimeoutException("The record committed but the response was lost.");
                }
            }
            return Task.FromResult(stored);
        }
        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class MemoryContent : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => Task.FromResult(new ImmutableContentRef(contentId,
                Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant(), bytes.Length, mediaType, schema));
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
