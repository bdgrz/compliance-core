using Cntryl.Fitz;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Handlers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

public sealed class SystemDescriptionExportProcessTests
{
    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task ShouldPublishVerifiedFormatAndExactSourceBytesGivenApprovedDescription(string format)
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync(approved: true);
        var request = Request(fixture, format, "verified-format");

        // Act
        var result = await new GenerateSystemDescriptionExportHandler(fixture.Exports).HandleAsync(fixture.Context(request), CancellationToken.None);
        var state = await ReadAsync(fixture, request);
        var output = await fixture.Content.ReadJsonAsync<SystemDescriptionExportOutput>(fixture.Tenant.ToString(), state.Output!);
        var exact = await fixture.Content.ReadVerifiedAsync(fixture.Tenant.ToString(), output.SourceContent);
        await using var opened = await fixture.Artifacts.OpenVerifiedAsync(output.Artifact);
        using var bytes = new MemoryStream();
        await opened!.CopyToAsync(bytes);
        var document = bytes.ToArray();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("completed", state.State);
        Assert.Equal(document.LongLength, output.Artifact.Length);
        Assert.Equal(Bdgrz.Compliance.Features.Kernel.Services.ContentDigest.Sha256(document), output.Artifact.Sha256);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
        if (format == "docx")
        {
            using var zip = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
            using var text = zip.GetEntry("word/document.xml")!.Open();
            var xml = XDocument.Load(text);
            XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var extracted = string.Join("\n", xml.Descendants(word + "t").Select(item => item.Value));
            Assert.Contains("Approved narrative", extracted, StringComparison.Ordinal);
            Assert.Contains("Exact narrative for information_data_flows", extracted, StringComparison.Ordinal);
            using var part = zip.GetEntry("customXml/item1.xml")!.Open();
            var source = XDocument.Load(part).Root!;
            Assert.Equal(exact.ToArray(), Convert.FromBase64String(source.Value));
            using var types = zip.GetEntry("[Content_Types].xml")!.Open();
            Assert.Equal("http://schemas.openxmlformats.org/package/2006/content-types", XDocument.Load(types).Root!.Name.NamespaceName);
        }
        else
        {
            Assert.True(document.AsSpan().StartsWith("%PDF-1.7"u8));
            Assert.True(document.AsSpan().EndsWith("%%EOF\n"u8));
            Assert.Contains("(Approved narrative) Tj", Encoding.ASCII.GetString(document), StringComparison.Ordinal);
            Assert.True(document.AsSpan().IndexOf(exact.Span) >= 0);
        }
        var artifacts = Environment.GetEnvironmentVariable("COMPLIANCE_EXPORT_RENDER_ARTIFACTS_DIR");
        if (!string.IsNullOrWhiteSpace(artifacts))
        {
            Directory.CreateDirectory(artifacts);
            await File.WriteAllBytesAsync(Path.Combine(artifacts, "system-description." + format), document);
            await File.WriteAllBytesAsync(Path.Combine(artifacts, "system-description-source.json"), exact.ToArray());
        }
    }

    [Theory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("connection")]
    public async Task ShouldResumePinnedApprovedBytesGivenUnknownAdmissionAndLaterDescriptionChange(string fault)
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync(approved: true);
        var store = new CommitThenThrowProcesses(fixture.Processes, throwOnAdmission: true, fault);
        var process = new SystemDescriptionExportProcess(store, fixture.Descriptions, fixture.Content, fixture.Artifacts, fixture.Clock);
        var handler = new GenerateSystemDescriptionExportHandler(process);
        var request = Request(fixture, "docx", "pinned-retry");
        var first = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        Assert.False(first.IsSuccess);
        Assert.True(first.Error!.IsTransient);
        var accepted = await ReadAsync(fixture, request);
        var original = await fixture.Content.ReadJsonAsync<SystemDescriptionExportIntent>(fixture.Tenant.ToString(), accepted.Intent);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        Assert.True((await new ProposeSystemDescriptionSuccessorHandler(fixture.Operations).HandleAsync(fixture.Context(new ProposeSystemDescriptionSuccessor(
            Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 3, "Later approved-source successor")), CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviseSystemDescriptionDraftHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Description, 4, "MUTABLE LATER TITLE", SystemDescriptionNativeFixture.Body().Sections, "Later title")), CancellationToken.None)).IsSuccess);

        // Act
        var retry = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var state = await ReadAsync(fixture, request);
        var output = await fixture.Content.ReadJsonAsync<SystemDescriptionExportOutput>(fixture.Tenant.ToString(), state.Output!);
        await using var opened = await fixture.Artifacts.OpenVerifiedAsync(output.Artifact);
        using var zip = new ZipArchive(opened!, ZipArchiveMode.Read);
        using var body = zip.GetEntry("word/document.xml")!.Open();
        var text = XDocument.Load(body).ToString();

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(original.SourceContent, output.SourceContent);
        Assert.Equal(original.Operation.AcceptedAt, output.Export.GeneratedAt);
        Assert.Equal("docx", output.Format);
        Assert.Equal(1, output.Version);
        Assert.Contains("Approved narrative", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MUTABLE LATER TITLE", text, StringComparison.Ordinal);
        Assert.Equal(5, (await fixture.FactsAsync()).Count);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("connection")]
    public async Task ShouldReplayCompletedOriginalUnitGivenUnknownCompletionOutcomeAndRestart(string fault)
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync(approved: true);
        var process = new SystemDescriptionExportProcess(new CommitThenThrowProcesses(fixture.Processes, throwOnAdmission: false, fault), fixture.Descriptions,
            fixture.Content, fixture.Artifacts, fixture.Clock);
        var request = Request(fixture, "pdf", "unknown-completion");

        // Act
        var first = await new GenerateSystemDescriptionExportHandler(process).HandleAsync(fixture.Context(request), CancellationToken.None);
        var state = await ReadAsync(fixture, request);
        var restarted = new SystemDescriptionExportProcess(new FitzKvSystemDescriptionExportProcesses(fixture.Kv), fixture.Descriptions, fixture.Content,
            fixture.Artifacts, fixture.Clock);
        var replay = await new GenerateSystemDescriptionExportHandler(restarted).HandleAsync(fixture.Context(request), CancellationToken.None);
        var after = await ReadAsync(fixture, request);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(state.Output, after.Output);
        Assert.Equal(state.OperationResult, after.OperationResult);
        Assert.Equal(1, after.Attempts);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRemainPendingAndRecoverForwardGivenBrokerOutageDuringUnknownCommitRecovery(bool admission)
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync(approved: true);
        var store = new CommitThenThrowProcesses(fixture.Processes, admission, "timeout", failRecoveryRead: true);
        var handler = new GenerateSystemDescriptionExportHandler(new SystemDescriptionExportProcess(store, fixture.Descriptions, fixture.Content,
            fixture.Artifacts, fixture.Clock));
        var request = Request(fixture, "docx", "recovery-outage");

        // Act
        var pending = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var retry = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var current = await ReadAsync(fixture, request);

        // Assert
        Assert.False(pending.IsSuccess);
        Assert.True(pending.Error!.IsTransient);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("completed", current.State);
        Assert.Equal(1, current.Attempts);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldFailClosedGivenRootIdentityReusedForAnotherDescriptionOrActor()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync(approved: true);
        var request = Request(fixture, "docx", "collision");
        var handler = new GenerateSystemDescriptionExportHandler(fixture.Exports);
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var before = await ReadAsync(fixture, request);

        // Act
        var owner = await handler.HandleAsync(fixture.Context(request with { DescriptionId = Uuid.CreateVersion4() }), CancellationToken.None);
        var actor = await handler.HandleAsync(fixture.Context(request, fixture.Reviewer), CancellationToken.None);
        var format = await handler.HandleAsync(fixture.Context(request with { Format = "pdf" }), CancellationToken.None);
        var freshId = await handler.HandleAsync(fixture.Context(request with { CommandId = Uuid.CreateVersion4() }), CancellationToken.None);
        var after = await ReadAsync(fixture, request);

        // Assert
        Assert.False(owner.IsSuccess);
        Assert.False(actor.IsSuccess);
        Assert.False(format.IsSuccess);
        Assert.False(freshId.IsSuccess);
        Assert.Equal(before, after);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldRetainUnicodeInDocxAndFreezeTypedPdfFailureGivenUnsupportedGlyph()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        const string narrative = "完整系统说明 🌍 café";
        await fixture.SeedAsync(approved: true, content: SystemDescriptionNativeFixture.Body(narrative));
        var pdf = Request(fixture, "pdf", "unicode-pdf");
        var handler = new GenerateSystemDescriptionExportHandler(fixture.Exports);

        // Act
        var failure = await handler.HandleAsync(fixture.Context(pdf), CancellationToken.None);
        var repeated = await handler.HandleAsync(fixture.Context(pdf), CancellationToken.None);
        var failed = await ReadAsync(fixture, pdf);
        var docx = Request(fixture, "docx", "unicode-docx");
        var generated = await handler.HandleAsync(fixture.Context(docx), CancellationToken.None);
        var state = await ReadAsync(fixture, docx);
        var output = await fixture.Content.ReadJsonAsync<SystemDescriptionExportOutput>(fixture.Tenant.ToString(), state.Output!);
        await using var opened = await fixture.Artifacts.OpenVerifiedAsync(output.Artifact);
        using var zip = new ZipArchive(opened!, ZipArchiveMode.Read);
        using var source = zip.GetEntry("word/document.xml")!.Open();
        var xml = XDocument.Load(source);

        // Assert
        Assert.False(failure.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, failure.Error!.Kind);
        Assert.Equal(failure.Error.Kind, repeated.Error!.Kind);
        Assert.Equal(failure.Error.Message, repeated.Error.Message);
        Assert.Equal(failure.Error.IsTransient, repeated.Error.IsTransient);
        Assert.Equal("failed", failed.State);
        Assert.Null(failed.Output);
        Assert.Null(failed.OperationResult);
        Assert.True(generated.IsSuccess, generated.Error?.Message);
        Assert.Contains(narrative, xml.ToString(), StringComparison.Ordinal);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    static GenerateSystemDescriptionExport Request(SystemDescriptionNativeFixture fixture, string format, string key) =>
        new(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 1, format, key);
    static async Task<SystemDescriptionExportProcessRecord> ReadAsync(SystemDescriptionNativeFixture fixture, GenerateSystemDescriptionExport request) =>
        (await fixture.Processes.ReadAsync(fixture.Tenant, SystemDescriptionExportProcess.IdFor(request.DescriptionId, request.Version, request.ExportKey), request.CommandId, CancellationToken.None))!;
    sealed class CommitThenThrowProcesses(ISystemDescriptionExportProcesses inner, bool throwOnAdmission, string fault = "io", bool failRecoveryRead = false) : ISystemDescriptionExportProcesses
    {
        int _thrown;
        int _recoveryRead;
        public Task<SystemDescriptionExportProcessRecord?> ReadAsync(Uuid tenant, Uuid process, Uuid command, CancellationToken ct)
        {
            if (failRecoveryRead && Interlocked.Exchange(ref _recoveryRead, 0) == 1)
                throw new ConnectionException("Outage during exact technical process recovery read");
            return inner.ReadAsync(tenant, process, command, ct);
        }
        Exception Failure()
        {
            Interlocked.Exchange(ref _recoveryRead, 1);
            return fault switch
            {
                "timeout" => new RequestTimeoutException("Unknown broker commit response"),
                "connection" => new ConnectionException("Disconnected after broker commit"),
                _ => new IOException("Unknown storage commit response"),
            };
        }
        public async Task<Result<SystemDescriptionExportProcessRecord>> AcceptIfAbsentAsync(SystemDescriptionExportProcessRecord candidate, CancellationToken ct)
        {
            var result = await inner.AcceptIfAbsentAsync(candidate, ct);
            if (throwOnAdmission && result.IsSuccess && Interlocked.Exchange(ref _thrown, 1) == 0)
                throw Failure();
            return result;
        }
        public async Task<Result<SystemDescriptionExportProcessRecord>> ReplaceAsync(SystemDescriptionExportProcessRecord expected, SystemDescriptionExportProcessRecord replacement, CancellationToken ct)
        {
            var result = await inner.ReplaceAsync(expected, replacement, ct);
            if (!throwOnAdmission && replacement.State == "completed" && result.IsSuccess && Interlocked.Exchange(ref _thrown, 1) == 0)
                throw Failure();
            return result;
        }
    }
}
