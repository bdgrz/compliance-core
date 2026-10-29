using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Cntryl.Portia;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests;

public sealed partial class AggregateCommandCoverageTests
{
    static readonly JsonSerializerOptions ExportJsonOptions = new()
    { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    // This is an explicit maintenance mode of the existing source inventory check. Ordinary tests only read the map.
    static void ExportInventoryIfRequested()
    {
        var destination = Environment.GetEnvironmentVariable("COMPLIANCE_COVERAGE_EXPORT_PATH");
        if (string.IsNullOrWhiteSpace(destination))
            return;
        var repository = FindRepositoryRoot();
        var sourcePath = Path.Combine(repository, "docs", "product", "aggregate-command-coverage.json");
        var root = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        var oldFacts = Rows(root, "facts", "fact");
        var oldRequests = Rows(root, "requests", "request");
        var oldAggregates = Rows(root, "aggregates", "aggregate");
        var oldUnwired = root["unwiredAggregateProducers"]!.AsArray().OfType<JsonObject>()
            .ToDictionary(UnwiredKey, static row => row, StringComparer.Ordinal);
        var sourceRoots = ReadSourceRoots();
        var typeSources = GetTypeSources(sourceRoots);
        var assemblies = ContractAssemblies();
        var factTypes = assemblies.SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && typeof(DomainEvent).IsAssignableFrom(type))
            .OrderBy(static type => type.Name, StringComparer.Ordinal).ToArray();
        var aggregateTypes = typeof(FindingAggregate).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract && typeof(Aggregate).IsAssignableFrom(type))
            .OrderBy(static type => type.Name, StringComparer.Ordinal).ToArray();
        var requestTypes = assemblies.SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && IsRequest(type))
            .OrderBy(static type => type.Name, StringComparer.Ordinal).ToArray();
        var handlerTypes = typeof(FindingAggregate).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract && type.GetInterfaces().Any(static item =>
                item.IsGenericType && item.GetGenericTypeDefinition().FullName is
                    "Cntryl.Portia.IRequestHandler`1" or "Cntryl.Portia.IRequestHandler`2")).ToArray();
        var factNames = factTypes.Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);
        var aggregateNames = aggregateTypes.Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);
        var helperNames = root["factEmissionHelpers"]!.AsArray().OfType<JsonObject>()
            .Select(static helper => helper["name"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var relevantTypes = aggregateNames.Concat(helperNames).ToHashSet(StringComparer.Ordinal);
        var publishers = AggregateFactPublisherNamesByType(sourceRoots, relevantTypes);
        var methodsByType = AggregateMethodsByType(sourceRoots, relevantTypes);
        var reducers = ReadCurrentReducers(sourceRoots, aggregateNames);
        var emitters = new HashSet<(string Fact, string Source)>();
        foreach (var (source, syntax) in sourceRoots)
            foreach (var type in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(type => relevantTypes.Contains(type.Identifier.ValueText)))
                foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
                    foreach (var fact in AggregateFactConstructionNames(method, factNames,
                                 publishers[type.Identifier.ValueText], methodsByType[type.Identifier.ValueText]))
                        emitters.Add((fact, source));
        var testReferences = ReadTestReferences(repository);

        var facts = new List<JsonObject>();
        foreach (var type in factTypes)
        {
            var row = CloneOrCreate(oldFacts, type.Name, "fact");
            var factReducers = reducers.Where(item => item.Fact == type.Name).OrderBy(static item => item.Aggregate,
                StringComparer.Ordinal).ThenBy(static item => item.Source, StringComparer.Ordinal).ToArray();
            var factEmitters = emitters.Where(item => item.Fact == type.Name).Select(static item => item.Source)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            row["contract"] = SourceOf(type.Name, typeSources);
            row["reducers"] = Nodes(factReducers.Select(static item => new JsonObject
            { ["aggregate"] = item.Aggregate, ["source"] = item.Source }));
            row["emitters"] = Strings(factEmitters);
            row["testSymbolReferences"] = Strings(testReferences.GetValueOrDefault(type.Name) ?? []);
            var priorDisposition = row["disposition"]?.GetValue<string>();
            if (factReducers.Length > 0 && factEmitters.Length > 0)
                row["disposition"] = priorDisposition == "migration_fact" ? priorDisposition : "aggregate_fact";
            else if (factReducers.Length == 0 && factEmitters.Length > 0)
            {
                if (priorDisposition != "audit_only")
                    throw new InvalidOperationException($"{type.Name} is emitted without a reducer; classify its genuine ownership gap before exporting.");
            }
            else if (factReducers.Length > 0)
            {
                row["disposition"] = "legacy_replay";
                if (string.IsNullOrWhiteSpace(row["note"]?.GetValue<string>()))
                    row["note"] = "Confirmed publisher gap: a current reducer exists but this source inventory finds no publishing operation. Retained for replay; this row does not claim a current write path.";
            }
            else
            {
                row["disposition"] = priorDisposition is "superseded_by_decision" or "superseded_vocabulary"
                    ? priorDisposition : "confirmed_gap";
                if (string.IsNullOrWhiteSpace(row["note"]?.GetValue<string>()))
                    row["note"] = "Confirmed source gap: no current aggregate reducer or emitter. Keep unresolved in the local delivery ledger; no implemented path is claimed.";
            }
            row["note"] ??= "";
            facts.Add(row);
        }
        root["facts"] = Nodes(facts);

        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Fitz:Endpoint"] = "ws://fitz:4090/ws", ["Fitz:ApplicationName"] = "compliance" }).Build(),
            developerAuthentication: true);
        var registered = services.Where(static descriptor => descriptor.ServiceType == typeof(RequestHandlerRegistration))
            .Select(static descriptor => (RequestHandlerRegistration)descriptor.ImplementationInstance!)
            .OrderBy(static registration => registration.RequestType.Name, StringComparer.Ordinal)
            .ThenBy(static registration => registration.HandlerType.Name, StringComparer.Ordinal).ToArray();
        var requests = new List<JsonObject>();
        foreach (var type in requestTypes)
        {
            var row = CloneOrCreate(oldRequests, type.Name, "request");
            var source = SourceOf(type.Name, typeSources);
            var registrations = registered.Where(item => item.RequestType == type).ToArray();
            var availableHandlers = handlerTypes.Where(handler => HandlesRequest(handler, type)).ToArray();
            var prior = row["classification"]?.GetValue<string>();
            var classification = registrations.Length > 0
                ? prior is "command" or "internal_reaction" or "query_or_reconstruction" ? prior
                    : IsReceiver(type) ? "internal_reaction" : IsReadRequest(type.Name) ? "query_or_reconstruction" : "command"
                : prior is "deferred_command" or "internal_executor_context" or "query_or_reconstruction" ? prior
                    : IsReadRequest(type.Name) ? "query_or_reconstruction" : "confirmed_command_gap";
            row["contract"] = source;
            row["classification"] = classification;
            row["interfaces"] = sourceRoots[source].DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(declaration => declaration.Identifier.ValueText == type.Name)
                .Select(static declaration => declaration.BaseList?.Types.ToString()).FirstOrDefault() ?? "";
            row["handlers"] = Nodes(registrations.Select(item => new JsonObject
            { ["name"] = item.HandlerType.Name, ["source"] = SourceOf(item.HandlerType.Name, typeSources), ["registered"] = true }));
            row["handlerRegistration"] = registrations.Length > 0;
            var helperServices = ExistingStrings(row, "helperServices").Where(typeSources.ContainsKey)
                .Concat(availableHandlers.SelectMany(static handler => handler.GetConstructors()
                    .SelectMany(static constructor => constructor.GetParameters())
                    .Select(static parameter => parameter.ParameterType.Name))
                    .Where(name => HelperHasAggregateOperations(name, typeSources, sourceRoots, aggregateNames)))
                .Where(typeSources.ContainsKey).Where(name => !aggregateNames.Contains(name))
                .Where(name => HelperHasAggregateOperations(name, typeSources, sourceRoots, aggregateNames))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            row["helperServices"] = Strings(helperServices);
            row["directAggregateReferences"] = Strings(availableHandlers.SelectMany(handler =>
                    typeSources.GetValueOrDefault(handler.Name) ?? []).SelectMany(path =>
                    sourceRoots[path].DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                        .Select(static creation => SimpleName(creation.Type)))
                .Where(aggregateNames.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            row["aggregateTargets"] = new JsonArray();
            row["testSymbolReferences"] = Strings(testReferences.GetValueOrDefault(type.Name) ?? []);
            if (classification is "confirmed_command_gap" or "deferred_command" &&
                string.IsNullOrWhiteSpace(row["note"]?.GetValue<string>()))
                row["note"] = "Confirmed source gap: no registered request handler. Keep unresolved in the local delivery ledger; no new GitHub issue or implemented path is claimed.";
            row["note"] ??= classification == "query_or_reconstruction"
                ? "Read/reconstruction request; it does not own a business mutation." : "";
            var unregistered = availableHandlers.Where(handler => !registrations.Any(item => item.HandlerType == handler))
                .Select(static handler => handler.Name).Order(StringComparer.Ordinal).ToArray();
            if (unregistered.Length > 0)
                row["unregisteredHandlerClasses"] = Strings(unregistered);
            else
                row.Remove("unregisteredHandlerClasses");
            requests.Add(row);
        }
        root["requests"] = Nodes(requests);
        using var seed = JsonDocument.Parse(root.ToJsonString());
        var aggregateMethods = ReadAggregateMethods(sourceRoots, aggregateNames);
        var producers = FindAggregateFactProducerOperations(seed.RootElement, sourceRoots, aggregateNames, aggregateMethods);
        var routeOperations = requests.ToDictionary(row => row["request"]!.GetValue<string>(), row =>
            row["classification"]!.GetValue<string>() is "command" or "internal_reaction" && row["handlerRegistration"]!.GetValue<bool>()
                ? AggregateMutationOperationsForRequest(JsonSerializer.SerializeToElement(row), sourceRoots,
                    typeSources, aggregateNames) : [], StringComparer.Ordinal);
        foreach (var row in requests)
        {
            var operations = routeOperations[row["request"]!.GetValue<string>()];
            row["aggregateTargets"] = Strings(operations.Where(operation => producers.Any(producer =>
                    producer.Aggregate == operation.Aggregate && AggregateMethodCanReach(operation.Method,
                        operation.ArgumentCount, producer.Method, producer.MethodArgumentCount, aggregateMethods[producer.Aggregate])))
                .Select(static operation => operation.Aggregate).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        }
        root["requests"] = Nodes(requests);
        var aggregates = new List<JsonObject>();
        foreach (var type in aggregateTypes)
        {
            var row = CloneOrCreate(oldAggregates, type.Name, "aggregate");
            row["source"] = SourceOf(type.Name, typeSources);
            row["appliedFacts"] = Strings(reducers.Where(item => item.Aggregate == type.Name)
                .Select(static item => item.Fact).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            foreach (var (classification, property) in new[] { ("command", "commandRequests"), ("internal_reaction", "internalReactionRequests") })
                row[property] = Strings(requests.Where(request => request["classification"]!.GetValue<string>() == classification &&
                        ExistingStrings(request, "aggregateTargets").Contains(type.Name, StringComparer.Ordinal))
                    .Select(static request => request["request"]!.GetValue<string>()).Order(StringComparer.Ordinal));
            row["testSymbolReferences"] = Strings(testReferences.GetValueOrDefault(type.Name) ?? []);
            if (row["commandRequests"]!.AsArray().Count > 0 || row["internalReactionRequests"]!.AsArray().Count > 0)
                row["commandRouteDisposition"] = "registered_command_route";
            else if (row["commandRouteDisposition"]?.GetValue<string>() != "read_only_legacy_replay")
            {
                row["commandRouteDisposition"] = "pending_command_route";
                row["ownerIssue"] ??= ExistingFamilyIssue(row["source"]!.GetValue<string>());
                row["note"] ??= "Confirmed source gap: no registered mutation route reaches this owner. Existing family tracks the boundary; retain the concrete blocker locally.";
            }
            aggregates.Add(row);
        }
        root["aggregates"] = Nodes(aggregates);

        var directCalls = FindAggregateOperationCalls(sourceRoots, aggregateNames);
        var unwired = new List<JsonObject>();
        foreach (var producer in producers.OrderBy(static producer => producer.Aggregate, StringComparer.Ordinal)
                     .ThenBy(static producer => producer.Method, StringComparer.Ordinal)
                     .ThenBy(static producer => producer.Fact, StringComparer.Ordinal))
        {
            if (routeOperations.Values.Any(operations => operations.Any(operation => operation.Aggregate == producer.Aggregate &&
                    AggregateMethodCanReach(operation.Method, operation.ArgumentCount, producer.Method,
                        producer.MethodArgumentCount, aggregateMethods[producer.Aggregate]))))
                continue;
            var identity = $"{producer.Fact}/{producer.Aggregate}/{producer.Method}/{producer.MethodArgumentCount}";
            var row = oldUnwired.TryGetValue(identity, out var previous) ? previous.DeepClone().AsObject() : new JsonObject { ["fact"] = producer.Fact };
            row["fact"] = producer.Fact;
            row["aggregate"] = producer.Aggregate;
            row["method"] = producer.Method;
            row["methodArgumentCount"] = producer.MethodArgumentCount;
            row["source"] = producer.Source;
            var callers = directCalls.Where(call => call.Aggregate == producer.Aggregate && call.Method == producer.Method &&
                    AggregateMethodCanReach(call.Method, call.ArgumentCount, producer.Method, producer.MethodArgumentCount,
                        aggregateMethods[producer.Aggregate])).GroupBy(static call => call.Source, StringComparer.Ordinal)
                .Select(group => new JsonObject
                {
                    ["source"] = group.Key,
                    ["methods"] = Strings(group.Select(static call => call.Method).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                })
                .OrderBy(static caller => caller["source"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
            row["callerSources"] = Nodes(callers);
            var oldClassification = row["classification"]?.GetValue<string>();
            row["classification"] = callers.Length == 0
                ? oldClassification == "legacy_emitter_to_prune" ? oldClassification : "no_registered_request_route"
                : oldClassification is "direct_reactor_executor" or "direct_processor_executor" ? oldClassification : "direct_processor_executor";
            row["ownerIssue"] ??= ExistingFamilyIssue(producer.Source);
            row["note"] ??= callers.Length == 0
                ? "Confirmed registered-request route gap; this source operation emits a fact but is not reached by a registered mutation handler. Keep unresolved locally."
                : "Source-derived direct caller path; a registered request route is not proven by this inventory. This is not a completed command path; retain the gap in the local ledger.";
            unwired.Add(row);
        }
        root["unwiredAggregateProducers"] = Nodes(unwired);
        var registeredNames = registered.Select(static item => item.HandlerType.Name).ToHashSet(StringComparer.Ordinal);
        root["counts"] = new JsonObject
        {
            ["domainEventContracts"] = facts.Count,
            ["factsWithReducer"] = facts.Count(static row => row["reducers"]!.AsArray().Count > 0),
            ["factsWithAggregateEmitter"] = facts.Count(static row => row["disposition"]!.GetValue<string>() == "aggregate_fact" && row["emitters"]!.AsArray().Count > 0),
            ["auditFactsWithoutReducer"] = facts.Count(static row => row["disposition"]!.GetValue<string>() == "audit_only"),
            ["factsWithoutReducerOrEmitter"] = facts.Count(static row => row["reducers"]!.AsArray().Count == 0 && row["emitters"]!.AsArray().Count == 0),
            ["legacyReducerOnlyFacts"] = facts.Count(static row => row["reducers"]!.AsArray().Count > 0 && row["emitters"]!.AsArray().Count == 0),
            ["aggregateTypes"] = aggregates.Count,
            ["aggregateTypesWithCommandPath"] = aggregates.Count(static row => row["commandRouteDisposition"]!.GetValue<string>() == "registered_command_route"),
            ["requestContracts"] = requests.Count,
            ["commonRequestContracts"] = requests.Count(static row => row["contract"]!.GetValue<string>().StartsWith("src/Compliance.Common/", StringComparison.Ordinal)),
            ["requestClassificationCounts"] = CountGroups(requests),
            ["applicationMcpAdapterRequests"] = root["applicationAdapters"]!.AsArray().Count,
            ["registeredCoreHandlerRegistrations"] = registered.Length,
            ["unregisteredCoreHandlerClasses"] = handlerTypes.Select(static type => type.Name).Distinct(StringComparer.Ordinal).Except(registeredNames, StringComparer.Ordinal).Count(),
            ["unwiredAggregateProducerOperations"] = unwired.Count,
            ["unwiredAggregateProducerClassifications"] = CountGroups(unwired),
            ["factReplayGuardRows"] = root["factReplayGuards"]!.AsArray().Count,
        };
        root["capturedAt"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var git = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
        { WorkingDirectory = repository, RedirectStandardOutput = true, UseShellExecute = false })!;
        root["sourceCommit"] = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        root["sourceWorktreeNote"] = "Inventory reflects the source and assemblies of this explicit export, including uncommitted source changes; sourceCommit identifies the current base commit.";
        var json = root.ToJsonString(ExportJsonOptions) + Environment.NewLine;
        File.WriteAllText(Path.GetFullPath(destination), json);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "aggregate-command-coverage.json"), json);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "compliance-coverage-handler-registry.json"), JsonSerializer.Serialize(registered.Select(item => new
        {
            request = item.RequestType.Name,
            requestSource = SourceOf(item.RequestType.Name, typeSources),
            handler = item.HandlerType.Name,
            handlerSource = SourceOf(item.HandlerType.Name, typeSources),
        }), ExportJsonOptions) + Environment.NewLine);
    }

    static Dictionary<string, JsonObject> Rows(JsonObject root, string property, string key) => root[property]!.AsArray()
        .OfType<JsonObject>().ToDictionary(row => row[key]!.GetValue<string>(), static row => row, StringComparer.Ordinal);

    static JsonObject CloneOrCreate(Dictionary<string, JsonObject> old, string name, string key) =>
        old.TryGetValue(name, out var row) ? row.DeepClone().AsObject() : new JsonObject { [key] = name };

    static JsonArray Nodes(IEnumerable<JsonObject> values) => [.. values.Select(static value => value.DeepClone())];
    static JsonArray Strings(IEnumerable<string> values) => [.. values.Select(static value => JsonValue.Create(value))];
    static IEnumerable<string> ExistingStrings(JsonObject row, string key) => row[key]?.AsArray()
        .Select(static node => node!.GetValue<string>()) ?? [];
    static JsonObject CountGroups(IEnumerable<JsonObject> rows) => new(rows.GroupBy(static row => row["classification"]!.GetValue<string>())
        .OrderBy(static group => group.Key, StringComparer.Ordinal).ToDictionary(static group => group.Key,
            static group => (JsonNode?)JsonValue.Create(group.Count()), StringComparer.Ordinal));
    static string UnwiredKey(JsonObject row) => $"{row["fact"]}/{row["aggregate"]}/{row["method"]}/{row["methodArgumentCount"]}";
    static bool IsReadRequest(string name) => name.StartsWith("Get", StringComparison.Ordinal) || name.StartsWith("List", StringComparison.Ordinal) ||
        name.StartsWith("Preview", StringComparison.Ordinal) || name.StartsWith("Search", StringComparison.Ordinal);
    static bool IsReceiver(Type type) => type.GetInterfaces().Any(static item => item.IsGenericType &&
        item.GetGenericTypeDefinition().FullName == "Cntryl.Portia.IRequest`1" && item.GetGenericArguments()[0].Name == "ReceiverOutcome");
    static string SourceOf(string name, Dictionary<string, string[]> sources) => sources.GetValueOrDefault(name)?
        .OrderBy(static path => path.StartsWith("src/Compliance.Common/", StringComparison.Ordinal) ? 0 : 1)
        .ThenBy(static path => path, StringComparer.Ordinal).FirstOrDefault()
        ?? throw new InvalidOperationException($"No current source path exists for {name}.");
    static int ExistingFamilyIssue(string source) => source.Contains("/AuditPackages/", StringComparison.Ordinal) || source.Contains("/AuditorRequests/", StringComparison.Ordinal) ||
        source.Contains("/AdvisorCollaboration/", StringComparison.Ordinal) || source.Contains("/Retention/", StringComparison.Ordinal) ? 1052 :
        source.Contains("/SystemDescriptions/", StringComparison.Ordinal) || source.Contains("/Templates/", StringComparison.Ordinal) ? 1053 : 1022;

    static bool HelperHasAggregateOperations(string name, Dictionary<string, string[]> typeSources,
        Dictionary<string, SyntaxNode> roots, HashSet<string> aggregateNames) =>
        (typeSources.GetValueOrDefault(name) ?? []).Any(source => roots[source].DescendantNodes()
            .OfType<TypeDeclarationSyntax>().Where(type => type.Identifier.ValueText == name).Any(type =>
                AggregateMutationOperations(type.Members.OfType<MethodDeclarationSyntax>()
                    .Select(method => (Method: method, Root: roots[source])), aggregateNames).Count > 0 ||
                type.Members.OfType<MethodDeclarationSyntax>().SelectMany(static method => method.ParameterList.Parameters)
                    .Any(parameter => parameter.Type is { } parameterType &&
                        TryResolveAggregateName(parameterType, roots[source], aggregateNames) is not null)));

    static HashSet<(string Fact, string Aggregate, string Source)> ReadCurrentReducers(Dictionary<string, SyntaxNode> roots, HashSet<string> aggregates)
    {
        var result = new HashSet<(string Fact, string Aggregate, string Source)>();
        foreach (var (source, root) in roots)
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var generic = call.Expression switch
                {
                    GenericNameSyntax name => name,
                    MemberAccessExpressionSyntax member when member.Name is GenericNameSyntax name => name,
                    _ => null,
                };
                if (generic?.Identifier.ValueText != "On" || generic.TypeArgumentList.Arguments.Count != 1)
                    continue;
                var owner = call.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault(type => aggregates.Contains(type.Identifier.ValueText));
                if (owner is not null)
                    result.Add((SimpleName(generic.TypeArgumentList.Arguments[0]), owner.Identifier.ValueText, source));
            }
        return result;
    }

    static Dictionary<string, string[]> ReadTestReferences(string repository)
    {
        var references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(repository, "test"), "*.cs", SearchOption.AllDirectories)
                     .Where(static path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal)))
        {
            var relative = Path.GetRelativePath(repository, path).Replace('\\', '/');
            foreach (var name in CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantTokens()
                         .Where(static token => token.IsKind(SyntaxKind.IdentifierToken)).Select(static token => token.ValueText)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!references.TryGetValue(name, out var paths))
                    references.Add(name, paths = []);
                paths.Add(relative);
            }
        }
        return references.ToDictionary(static pair => pair.Key,
            static pair => pair.Value.Order(StringComparer.Ordinal).Take(8).ToArray(), StringComparer.Ordinal);
    }
}
