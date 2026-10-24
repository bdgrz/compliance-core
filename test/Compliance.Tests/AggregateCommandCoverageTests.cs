using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests;

public sealed class AggregateCommandCoverageTests
{
    [Fact]
    public void ShouldInventoryEveryDomainEventContractGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var expected = inventory.RootElement.GetProperty("facts").EnumerateArray()
            .Select(static fact => fact.GetProperty("fact").GetString()!).ToHashSet(StringComparer.Ordinal);
        var actual = ContractAssemblies().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract &&
                typeof(DomainEvent).IsAssignableFrom(type))
            .Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);

        // Act

        // Assert
        Assert.Equal(actual.Order(StringComparer.Ordinal), expected.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ShouldInventoryEveryRequestContractGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var expected = inventory.RootElement.GetProperty("requests").EnumerateArray()
            .Select(static request => request.GetProperty("request").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var actual = ContractAssemblies().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && IsRequest(type))
            .Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);

        // Act

        // Assert
        Assert.Equal(actual.Order(StringComparer.Ordinal), expected.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ShouldClassifyEveryDomainFactDispositionGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var failures = new List<string>();

        // Act
        foreach (var fact in inventory.RootElement.GetProperty("facts").EnumerateArray())
        {
            var name = fact.GetProperty("fact").GetString()!;
            var disposition = fact.GetProperty("disposition").GetString();
            var hasReducer = fact.GetProperty("reducers").GetArrayLength() > 0;
            var hasEmitter = fact.GetProperty("emitters").GetArrayLength() > 0;
            var hasNote = !string.IsNullOrWhiteSpace(fact.GetProperty("note").GetString());
            var valid = disposition switch
            {
                "aggregate_fact" => hasReducer && hasEmitter,
                "audit_only" => !hasReducer && hasEmitter && hasNote,
                "legacy_replay" => hasReducer && !hasEmitter && hasNote,
                "confirmed_gap" => !hasReducer && !hasEmitter && hasNote,
                "migration_fact" => hasReducer && hasEmitter && hasNote,
                "superseded_by_decision" or "superseded_vocabulary" =>
                    !hasReducer && !hasEmitter && hasNote,
                _ => false,
            };
            if (!valid)
                failures.Add($"{name}: invalid or missing fact disposition {disposition}");
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ShouldInventoryEveryAggregateAndReachItThroughARegisteredCommandGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var root = inventory.RootElement;
        var aggregateRows = root.GetProperty("aggregates").EnumerateArray().ToArray();
        var expected = aggregateRows.Select(static aggregate =>
            aggregate.GetProperty("aggregate").GetString()!).ToHashSet(StringComparer.Ordinal);
        var expectedRouted = aggregateRows.Where(static aggregate =>
                aggregate.GetProperty("commandRouteDisposition").GetString() == "registered_command_route")
            .Select(static aggregate => aggregate.GetProperty("aggregate").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var explicitlyReadOnly = aggregateRows.Where(static aggregate =>
                aggregate.GetProperty("commandRouteDisposition").GetString() == "read_only_legacy_replay")
            .Select(static aggregate => aggregate.GetProperty("aggregate").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var actual = typeof(FindingAggregate).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract &&
                typeof(Aggregate).IsAssignableFrom(type))
            .Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);
        var requests = root.GetProperty("requests").EnumerateArray().ToArray();
        var sourceRoots = ReadSourceRoots();
        var aggregateNames = actual;
        var typeSources = GetTypeSources(sourceRoots);
        var aggregateMethods = ReadAggregateMethods(sourceRoots, aggregateNames);
        var factProducers = FindAggregateFactProducerOperations(root, sourceRoots, aggregateNames,
            aggregateMethods);
        var producerOperations = factProducers;

        // Act
        var requestMutations = requests.Where(request =>
                request.GetProperty("classification").GetString() is "command" or "internal_reaction" &&
                request.GetProperty("handlerRegistration").GetBoolean())
            .SelectMany(request => AggregateMutationOperationsForRequest(request, sourceRoots,
                typeSources, aggregateNames).Where(operation => producerOperations.Any(producer =>
                    producer.Aggregate == operation.Aggregate && AggregateMethodCanReach(
                        operation.Method, operation.ArgumentCount, producer.Method,
                        producer.MethodArgumentCount, aggregateMethods[producer.Aggregate]))))
            .Select(static operation => operation.Aggregate).ToHashSet(StringComparer.Ordinal);
        var missingPaths = expectedRouted.Except(requestMutations).ToArray();
        var undeclaredRoutedPaths = requestMutations.Except(expectedRouted).ToArray();
        var invalidReadOnlyRoots = explicitlyReadOnly.Where(requestMutations.Contains).ToArray();
        var pendingRoutesWithoutEvidence = aggregateRows.Where(static aggregate =>
                aggregate.GetProperty("commandRouteDisposition").GetString() ==
                    "pending_command_route")
            .Where(static aggregate =>
                aggregate.GetProperty("ownerIssue").ValueKind != JsonValueKind.Number ||
                string.IsNullOrWhiteSpace(aggregate.GetProperty("note").GetString()) ||
                aggregate.GetProperty("commandRequests").GetArrayLength() != 0 ||
                aggregate.GetProperty("internalReactionRequests").GetArrayLength() != 0)
            .Select(static aggregate => aggregate.GetProperty("aggregate").GetString()!).ToArray();
        var missingDisposition = aggregateRows.Where(static aggregate =>
                aggregate.GetProperty("commandRouteDisposition").GetString() is not
                    ("registered_command_route" or "read_only_legacy_replay" or
                     "pending_command_route"))
            .Select(static aggregate => aggregate.GetProperty("aggregate").GetString()!).ToArray();

        // Assert
        Assert.Equal(actual.Order(StringComparer.Ordinal), expected.Order(StringComparer.Ordinal));
        Assert.Empty(missingPaths);
        Assert.Empty(undeclaredRoutedPaths);
        Assert.Empty(invalidReadOnlyRoots);
        Assert.Empty(missingDisposition);
        Assert.Empty(pendingRoutesWithoutEvidence);
        Assert.Equal(expectedRouted.Count, root.GetProperty("counts")
            .GetProperty("aggregateTypesWithCommandPath").GetInt32());
    }

    [Fact]
    public void ShouldKeepAggregateCommandRoutesConsistentInBothDirectionsGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var aggregates = inventory.RootElement.GetProperty("aggregates").EnumerateArray()
            .ToDictionary(static aggregate => aggregate.GetProperty("aggregate").GetString()!,
                StringComparer.Ordinal);
        var requests = inventory.RootElement.GetProperty("requests").EnumerateArray()
            .ToDictionary(static request => request.GetProperty("request").GetString()!,
                StringComparer.Ordinal);
        var aggregateNames = aggregates.Keys.ToHashSet(StringComparer.Ordinal);
        var sourceRoots = ReadSourceRoots();
        var typeSources = GetTypeSources(sourceRoots);
        var aggregateMethods = ReadAggregateMethods(sourceRoots, aggregateNames);
        var factProducers = FindAggregateFactProducerOperations(inventory.RootElement, sourceRoots,
            aggregateNames, aggregateMethods);
        var producerOperations = factProducers;
        var actualRequestTargets = requests.Values.ToDictionary(
            static request => request.GetProperty("request").GetString()!,
            request => request.GetProperty("classification").GetString() is
                    "command" or "internal_reaction"
                ? AggregateMutationOperationsForRequest(request, sourceRoots, typeSources,
                        aggregateNames).Where(operation => producerOperations.Any(producer =>
                            producer.Aggregate == operation.Aggregate && AggregateMethodCanReach(
                                operation.Method, operation.ArgumentCount, producer.Method,
                                producer.MethodArgumentCount, aggregateMethods[producer.Aggregate])))
                    .Select(static operation => operation.Aggregate)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : [], StringComparer.Ordinal);
        var failures = new List<string>();

        // Act
        foreach (var aggregate in aggregates.Values)
        {
            var aggregateName = aggregate.GetProperty("aggregate").GetString()!;
            CheckRoutes(aggregate, "commandRequests", "command", aggregateName, requests,
                sourceRoots, typeSources, aggregateNames, aggregateMethods, producerOperations,
                failures);
            CheckRoutes(aggregate, "internalReactionRequests", "internal_reaction", aggregateName,
                requests, sourceRoots, typeSources, aggregateNames, aggregateMethods,
                producerOperations, failures);
        }
        foreach (var request in requests.Values)
        {
            var requestName = request.GetProperty("request").GetString()!;
            var classification = request.GetProperty("classification").GetString();
            if (classification is not ("command" or "internal_reaction"))
                continue;
            var aggregateProperty = classification == "command"
                ? "commandRequests"
                : "internalReactionRequests";
            var actualTargets = actualRequestTargets[requestName].ToHashSet(StringComparer.Ordinal);
            var mappedTargets = request.GetProperty("aggregateTargets").EnumerateArray()
                .Select(static item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
            foreach (var target in mappedTargets.Except(actualTargets))
                failures.Add($"{requestName}: {target} is listed as a mutation target but no registered handler route mutates it");
            foreach (var target in actualTargets.Except(mappedTargets))
                failures.Add($"{requestName}: {target} is mutated by a registered handler route but is missing from aggregateTargets");
            foreach (var target in actualTargets)
            {
                if (!aggregates.TryGetValue(target, out var aggregate) ||
                    !aggregate.GetProperty(aggregateProperty).EnumerateArray()
                        .Any(item => item.GetString() == requestName))
                    failures.Add($"{requestName}: {target} is missing its {classification} route");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ShouldMatchReducerAndEmitterRowsToCSharpSourceGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var root = inventory.RootElement;
        var aggregateNames = root.GetProperty("aggregates").EnumerateArray()
            .Select(static aggregate => aggregate.GetProperty("aggregate").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var helperRows = root.GetProperty("factEmissionHelpers").EnumerateArray().ToArray();
        var helperNames = helperRows.Select(static helper =>
            helper.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
        var facts = root.GetProperty("facts").EnumerateArray().ToArray();
        var factNames = facts.Select(static fact => fact.GetProperty("fact").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var sourceRoots = ReadSourceRoots();
        var relevantTypes = aggregateNames.Concat(helperNames).ToHashSet(StringComparer.Ordinal);
        var publisherNamesByType = AggregateFactPublisherNamesByType(sourceRoots, relevantTypes);
        var methodsByType = AggregateMethodsByType(sourceRoots, relevantTypes);
        var actualReducers = new HashSet<(string Fact, string Aggregate, string Source)>();
        var actualEmitters = new HashSet<(string Fact, string Source)>();

        // Act
        foreach (var (relativePath, syntaxRoot) in sourceRoots)
        {
            foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var generic = invocation.Expression switch
                {
                    GenericNameSyntax name => name,
                    MemberAccessExpressionSyntax member when member.Name is GenericNameSyntax name => name,
                    _ => null,
                };
                if (generic?.Identifier.ValueText != "On" ||
                    generic.TypeArgumentList.Arguments.Count != 1)
                    continue;
                var aggregate = invocation.Ancestors().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(type => aggregateNames.Contains(type.Identifier.ValueText));
                if (aggregate is null)
                    continue;
                actualReducers.Add((SimpleName(generic.TypeArgumentList.Arguments[0]),
                    aggregate.Identifier.ValueText, relativePath));
            }
            foreach (var aggregate in syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(type => aggregateNames.Contains(type.Identifier.ValueText) ||
                             helperNames.Contains(type.Identifier.ValueText)))
            {
                var publisherNames = publisherNamesByType[aggregate.Identifier.ValueText];
                foreach (var method in aggregate.Members.OfType<MethodDeclarationSyntax>())
                    foreach (var fact in AggregateFactConstructionNames(method, factNames,
                                 publisherNames, methodsByType[aggregate.Identifier.ValueText]))
                        actualEmitters.Add((fact, relativePath));
            }
        }
        var expectedReducers = facts.SelectMany(fact => fact.GetProperty("reducers").EnumerateArray()
            .Select(reducer => (Fact: fact.GetProperty("fact").GetString()!,
                Aggregate: reducer.GetProperty("aggregate").GetString()!,
                Source: reducer.GetProperty("source").GetString()!))).ToHashSet();
        var expectedEmitters = facts.SelectMany(fact => fact.GetProperty("emitters").EnumerateArray()
            .Select(emitter => (Fact: fact.GetProperty("fact").GetString()!,
                Source: emitter.GetString()!))).ToHashSet();
        var missingReducers = expectedReducers.Except(actualReducers).ToArray();
        var extraReducers = actualReducers.Except(expectedReducers).ToArray();
        var missingEmitters = expectedEmitters.Except(actualEmitters).ToArray();
        var extraEmitters = actualEmitters.Except(expectedEmitters).ToArray();

        // Assert
        Assert.True(missingReducers.Length == 0 && extraReducers.Length == 0,
            "Reducer inventory differs from source." + Environment.NewLine +
            "Missing: " + string.Join(", ", missingReducers.Select(static row =>
                $"{row.Fact}/{row.Aggregate}/{row.Source}")) + Environment.NewLine +
            "Extra: " + string.Join(", ", extraReducers.Select(static row =>
                $"{row.Fact}/{row.Aggregate}/{row.Source}")));
        Assert.True(missingEmitters.Length == 0 && extraEmitters.Length == 0,
            "Emitter inventory differs from source." + Environment.NewLine +
            "Missing: " + string.Join(", ", missingEmitters.Select(static row =>
                $"{row.Fact}/{row.Source}")) + Environment.NewLine +
            "Extra: " + string.Join(", ", extraEmitters.Select(static row =>
                $"{row.Fact}/{row.Source}")));
    }

    [Fact]
    public void ShouldRequireConstructedFactsToReachAnEventPublisherGivenAggregateMethods()
    {
        // Arrange
        const string source = """
            class SampleAggregate
            {
                void Discard()
                {
                    _ = new SampleFact();
                }

                void PublishDirect()
                {
                    RaiseEvent(new SampleFact());
                }

                void PublishTargetTypedDirect()
                {
                    RaiseSampleFact(new());
                }

                void RaiseSampleFact(SampleFact fact)
                {
                    RaiseEvent(fact);
                }

                void PublishTargetTypedCallback(System.Action<SampleFact> publish)
                {
                    publish(new());
                }

                void PublishTargetTypedCallbackInsideLocal(System.Action<SampleFact> publish)
                {
                    var task = Task.Run(() => publish(new()));
                }

                void PublishLocal()
                {
                    var fact = new SampleFact();
                    RaiseEvent(fact);
                }

                void PublishFactory()
                {
                    Publish(() => new SampleFact());
                }

                void Publish(System.Func<SampleFact> factory)
                {
                    RaiseEvent(factory());
                }

                SampleFact Prepare() => new SampleFact();

                void PublishPrepared()
                {
                    var prepared = Prepare();
                    RaiseEvent(prepared);
                }

                SampleFact PrepareUnused() => new SampleFact();

                void RaiseEvent(object fact) { }
            }
            """;
        var aggregate = CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var factNames = new HashSet<string>(["SampleFact"], StringComparer.Ordinal);
        var methods = aggregate.Members.OfType<MethodDeclarationSyntax>()
            .ToDictionary(static method => method.Identifier.ValueText, StringComparer.Ordinal);
        var publisherNames = AggregateFactPublisherNames([aggregate]);
        var allMethods = aggregate.Members.OfType<MethodDeclarationSyntax>().ToArray();

        // Act
        var discarded = AggregateFactConstructionNames(methods["Discard"], factNames,
            publisherNames, allMethods);
        var direct = AggregateFactConstructionNames(methods["PublishDirect"], factNames,
            publisherNames, allMethods);
        var targetTypedDirect = AggregateFactConstructionNames(methods["PublishTargetTypedDirect"],
            factNames, publisherNames, allMethods);
        var targetTypedCallback = AggregateFactConstructionNames(
            methods["PublishTargetTypedCallback"], factNames, publisherNames, allMethods);
        var targetTypedCallbackInsideLocal = AggregateFactConstructionNames(
            methods["PublishTargetTypedCallbackInsideLocal"], factNames, publisherNames, allMethods);
        var local = AggregateFactConstructionNames(methods["PublishLocal"], factNames,
            publisherNames, allMethods);
        var factory = AggregateFactConstructionNames(methods["PublishFactory"], factNames,
            publisherNames, allMethods);
        var prepared = AggregateFactConstructionNames(methods["Prepare"], factNames,
            publisherNames, allMethods);
        var unusedPrepared = AggregateFactConstructionNames(methods["PrepareUnused"], factNames,
            publisherNames, allMethods);

        // Assert
        Assert.Empty(discarded);
        Assert.Equal(factNames, direct);
        Assert.Equal(factNames, targetTypedDirect);
        Assert.Equal(factNames, targetTypedCallback);
        Assert.Equal(factNames, targetTypedCallbackInsideLocal);
        Assert.Equal(factNames, local);
        Assert.Equal(factNames, factory);
        Assert.Equal(factNames, prepared);
        Assert.Empty(unusedPrepared);
    }

    [Fact]
    public void ShouldDerivePublishedCountsFromInventoryRowsGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var root = inventory.RootElement;
        var counts = root.GetProperty("counts");
        var facts = root.GetProperty("facts").EnumerateArray().ToArray();
        var aggregates = root.GetProperty("aggregates").EnumerateArray().ToArray();
        var replayGuards = root.GetProperty("factReplayGuards").EnumerateArray().ToArray();
        var requests = root.GetProperty("requests").EnumerateArray().ToArray();
        var producers = root.GetProperty("unwiredAggregateProducers").EnumerateArray().ToArray();
        var handlerTypes = typeof(FindingAggregate).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract &&
                type.GetInterfaces().Any(static @interface => @interface.IsGenericType &&
                    @interface.GetGenericTypeDefinition().FullName is
                        "Cntryl.Portia.IRequestHandler`1" or "Cntryl.Portia.IRequestHandler`2"))
            .Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);
        var registeredHandlerNames = requests.SelectMany(static request =>
                request.GetProperty("handlers").EnumerateArray())
            .Where(static handler => handler.GetProperty("registered").GetBoolean())
            .Select(static handler => handler.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var requestClassifications = requests.GroupBy(static request =>
                request.GetProperty("classification").GetString()!)
            .ToDictionary(static group => group.Key, static group => group.Count(),
                StringComparer.Ordinal);
        var producerClassifications = producers.GroupBy(static producer =>
                producer.GetProperty("classification").GetString()!)
            .ToDictionary(static group => group.Key, static group => group.Count(),
                StringComparer.Ordinal);

        // Assert
        void AssertCount(string name, int expected) =>
            Assert.Equal(expected, counts.GetProperty(name).GetInt32());

        void AssertCounts(string name, Dictionary<string, int> expected)
        {
            var actual = counts.GetProperty(name).EnumerateObject()
                .ToDictionary(static property => property.Name,
                    static property => property.Value.GetInt32(), StringComparer.Ordinal);
            Assert.Equal(expected.OrderBy(static item => item.Key, StringComparer.Ordinal),
                actual.OrderBy(static item => item.Key, StringComparer.Ordinal));
        }

        AssertCount("domainEventContracts", facts.Length);
        AssertCount("factsWithReducer", facts.Count(static fact =>
            fact.GetProperty("reducers").GetArrayLength() > 0));
        AssertCount("factsWithAggregateEmitter", facts.Count(static fact =>
            fact.GetProperty("disposition").GetString() == "aggregate_fact" &&
            fact.GetProperty("emitters").GetArrayLength() > 0));
        AssertCount("auditFactsWithoutReducer", facts.Count(static fact =>
            fact.GetProperty("disposition").GetString() == "audit_only"));
        AssertCount("factsWithoutReducerOrEmitter", facts.Count(static fact =>
            fact.GetProperty("reducers").GetArrayLength() == 0 &&
            fact.GetProperty("emitters").GetArrayLength() == 0));
        AssertCount("legacyReducerOnlyFacts", facts.Count(static fact =>
            fact.GetProperty("reducers").GetArrayLength() > 0 &&
            fact.GetProperty("emitters").GetArrayLength() == 0));
        AssertCount("aggregateTypes", aggregates.Length);
        AssertCount("factReplayGuardRows", replayGuards.Length);
        AssertCount("aggregateTypesWithCommandPath", aggregates.Count(static aggregate =>
            aggregate.GetProperty("commandRouteDisposition").GetString() ==
            "registered_command_route"));
        AssertCount("requestContracts", requests.Length);
        AssertCount("commonRequestContracts", requests.Count(static request =>
            request.GetProperty("contract").GetString()!.StartsWith(
                "src/Compliance.Common/", StringComparison.Ordinal)));
        AssertCounts("requestClassificationCounts", requestClassifications);
        AssertCount("applicationMcpAdapterRequests",
            root.GetProperty("applicationAdapters").GetArrayLength());
        AssertCount("registeredCoreHandlerRegistrations", requests.Sum(static request =>
            request.GetProperty("handlers").EnumerateArray()
                .Count(static handler => handler.GetProperty("registered").GetBoolean())));
        AssertCount("unregisteredCoreHandlerClasses",
            handlerTypes.Except(registeredHandlerNames, StringComparer.Ordinal).Count());
        AssertCount("unwiredAggregateProducerOperations", producers.Length);
        AssertCounts("unwiredAggregateProducerClassifications", producerClassifications);
    }

    [Fact]
    public void ShouldReachEveryAggregateFactEmitterThroughARegisteredRequestGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var root = inventory.RootElement;
        var aggregateRows = root.GetProperty("aggregates").EnumerateArray().ToArray();
        var aggregateNames = aggregateRows.Select(static aggregate =>
            aggregate.GetProperty("aggregate").GetString()!).ToHashSet(StringComparer.Ordinal);
        var factRows = root.GetProperty("facts").EnumerateArray()
            .ToDictionary(static fact => fact.GetProperty("fact").GetString()!, StringComparer.Ordinal);
        var aggregateFactNames = factRows.Where(static fact =>
                fact.Value.GetProperty("reducers").GetArrayLength() > 0)
            .Select(static fact => fact.Key).ToHashSet(StringComparer.Ordinal);
        var repositoryRoot = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs",
            SearchOption.AllDirectories).ToArray();
        var sourceRoots = sourceFiles.ToDictionary(
            path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
            path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot(),
            StringComparer.Ordinal);
        var typeSources = GetTypeSources(sourceRoots);
        var requests = root.GetProperty("requests").EnumerateArray().ToArray();
        var aggregateMethods = ReadAggregateMethods(sourceRoots, aggregateNames);
        var producerMethods = FindAggregateFactProducerOperations(root, sourceRoots,
            aggregateNames, aggregateMethods);

        // Act
        var actualUnwired = new HashSet<(string Fact, string Aggregate, string Method,
            int MethodArgumentCount, string Source)>();
        var invalidAggregateMethodEvidence = new List<string>();
        foreach (var producer in producerMethods)
            if (aggregateMethods.TryGetValue(producer.Aggregate, out var methods) &&
                methods.Count(method => method.IsCallable && !method.IsForwardingOverload &&
                    method.Name == producer.Method &&
                    method.ParameterCount == producer.MethodArgumentCount) != 1)
                invalidAggregateMethodEvidence.Add(
                    $"{producer.Aggregate}.{producer.Method}/{producer.MethodArgumentCount} is overloaded; route evidence needs a signature");

        foreach (var producer in producerMethods)
        {
            var reachable = false;
            foreach (var request in requests.Where(request =>
                         request.GetProperty("classification").GetString() is "command" or "internal_reaction" &&
                         request.GetProperty("handlerRegistration").GetBoolean()))
            {
                var routeOperations = AggregateMutationOperationsForRequest(request, sourceRoots,
                    typeSources, aggregateNames);
                if (routeOperations.Any(operation => operation.Aggregate == producer.Aggregate &&
                    AggregateMethodCanReach(operation.Method, operation.ArgumentCount,
                        producer.Method, producer.MethodArgumentCount,
                        aggregateMethods.GetValueOrDefault(producer.Aggregate) ?? [])))
                {
                    reachable = true;
                    break;
                }
            }

            if (!reachable)
                actualUnwired.Add(producer);
        }
        var unwiredRows = root.GetProperty("unwiredAggregateProducers").EnumerateArray().ToArray();
        var expectedUnwired = unwiredRows
            .Select(producer => (Fact: producer.GetProperty("fact").GetString()!,
                Aggregate: producer.GetProperty("aggregate").GetString()!,
                Method: producer.GetProperty("method").GetString()!,
                MethodArgumentCount: ReadMethodArgumentCount(producer, aggregateMethods),
                Source: producer.GetProperty("source").GetString()!)).ToHashSet();
        var routeCounts = root.GetProperty("counts");
        var expectedRouteGapCounts = unwiredRows.GroupBy(row =>
                row.GetProperty("classification").GetString()!)
            .ToDictionary(static group => group.Key, static group => group.Count(),
                StringComparer.Ordinal);
        var missingPaths = expectedUnwired.Except(actualUnwired).ToArray();
        var extraPaths = actualUnwired.Except(expectedUnwired).ToArray();
        var invalidGapEvidence = new List<string>();
        var actualDirectCalls = FindAggregateOperationCalls(sourceRoots, aggregateNames);
        var actualUnwiredInventory = actualUnwired.Select(producer => new
        {
            producer.Fact,
            producer.Aggregate,
            producer.Method,
            producer.MethodArgumentCount,
            producer.Source,
            CallerSources = actualDirectCalls.Where(call =>
                    call.Aggregate == producer.Aggregate && call.Method == producer.Method &&
                    AggregateMethodCanReach(call.Method, call.ArgumentCount, producer.Method,
                        producer.MethodArgumentCount,
                        aggregateMethods.GetValueOrDefault(producer.Aggregate) ?? []))
                    .GroupBy(static call => call.Source, StringComparer.Ordinal)
                    .Select(group => new
                    {
                        Source = group.Key,
                        Methods = group.Select(static call => call.Method)
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    }).OrderBy(static caller => caller.Source, StringComparer.Ordinal).ToArray(),
        }).OrderBy(static producer => producer.Aggregate, StringComparer.Ordinal)
            .ThenBy(static producer => producer.Method, StringComparer.Ordinal)
            .ThenBy(static producer => producer.Fact, StringComparer.Ordinal).ToArray();
        foreach (var producer in unwiredRows)
        {
            var fact = producer.GetProperty("fact").GetString()!;
            var aggregate = producer.GetProperty("aggregate").GetString()!;
            var method = producer.GetProperty("method").GetString()!;
            var methodArgumentCount = ReadMethodArgumentCount(producer, aggregateMethods);
            var classification = producer.GetProperty("classification").GetString();
            var ownerIssue = producer.GetProperty("ownerIssue").ValueKind == JsonValueKind.Number;
            var followUpIssueNeeded = producer.TryGetProperty("followUpIssueNeeded", out var needed) &&
                needed.GetBoolean();
            var followUpReason = producer.TryGetProperty("followUpReason", out var reason) &&
                !string.IsNullOrWhiteSpace(reason.GetString());
            var callerRows = producer.GetProperty("callerSources").EnumerateArray().ToArray();
            var callerSources = callerRows.Select(static caller =>
                caller.GetProperty("source").GetString()!).ToArray();
            if (classification is not ("direct_reactor_executor" or "direct_processor_executor" or
                    "no_registered_request_route" or "legacy_emitter_to_prune"))
                invalidGapEvidence.Add($"{fact}: unknown producer route classification {classification}");
            if (!ownerIssue && !(followUpIssueNeeded && followUpReason))
                invalidGapEvidence.Add($"{fact}: missing owning issue or follow-up issue disposition");
            var expectedDirectCalls = actualDirectCalls.Where(call =>
                    call.Aggregate == aggregate && call.Method == method &&
                    AggregateMethodCanReach(call.Method, call.ArgumentCount, method,
                        methodArgumentCount, aggregateMethods.GetValueOrDefault(aggregate) ?? []))
                .GroupBy(static call => call.Source, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key,
                    static group => group.Select(static call => call.Method)
                        .ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
            var listedDirectCalls = callerRows.ToDictionary(static caller =>
                    caller.GetProperty("source").GetString()!,
                static caller => caller.GetProperty("methods").EnumerateArray()
                    .Select(static item => item.GetString()!).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
            if (expectedDirectCalls.Count != listedDirectCalls.Count ||
                expectedDirectCalls.Any(expected => !listedDirectCalls.TryGetValue(expected.Key,
                    out var actual) || !expected.Value.SetEquals(actual)))
                invalidGapEvidence.Add($"{fact}/{aggregate}.{method}: direct caller evidence differs from source");
            if (classification == "no_registered_request_route" && expectedDirectCalls.Count != 0)
                invalidGapEvidence.Add($"{fact}/{aggregate}.{method}: no-route classification has source callers");
            if (classification == "legacy_emitter_to_prune" &&
                (expectedDirectCalls.Count != 0 ||
                 !producer.TryGetProperty("migrationRequired", out var migrationRequired) ||
                 !migrationRequired.GetBoolean() ||
                 !producer.TryGetProperty("migrationDisposition", out var migrationDisposition) ||
                 migrationDisposition.GetString() != "retain_until_verified_stream_migration"))
                invalidGapEvidence.Add($"{fact}/{aggregate}.{method}: legacy emitter must have no source caller and retain replay compatibility until a verified stream migration");
            if (classification is "direct_reactor_executor" or "direct_processor_executor" &&
                expectedDirectCalls.Count == 0)
                invalidGapEvidence.Add($"{fact}/{aggregate}.{method}: direct executor path has no source caller");
            foreach (var caller in callerRows)
            {
                var callerSource = caller.GetProperty("source").GetString()!;
                var callerMethods = caller.GetProperty("methods").EnumerateArray()
                    .Select(static item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
                if (!sourceRoots.TryGetValue(callerSource, out _) ||
                    !expectedDirectCalls.TryGetValue(callerSource, out var actualMethods) ||
                    !callerMethods.SetEquals(actualMethods))
                    invalidGapEvidence.Add($"{fact}: {callerSource} does not call a listed aggregate operation");
            }
        }

        // Assert
        Assert.True(missingPaths.Length == 0 && extraPaths.Length == 0 &&
            invalidGapEvidence.Count == 0 && invalidAggregateMethodEvidence.Count == 0 &&
            routeCounts.GetProperty("unwiredAggregateProducerOperations").GetInt32() ==
                expectedUnwired.Count &&
            routeCounts.GetProperty("unwiredAggregateProducerClassifications").EnumerateObject()
                .ToDictionary(static entry => entry.Name, static entry => entry.Value.GetInt32(),
                    StringComparer.Ordinal).OrderBy(static item => item.Key, StringComparer.Ordinal)
                .SequenceEqual(expectedRouteGapCounts.OrderBy(static item => item.Key,
                    StringComparer.Ordinal)),
            "Unwired aggregate producer inventory differs from registered request reachability." +
            Environment.NewLine + "Missing: " + string.Join(", ", missingPaths.Select(static row =>
                $"{row.Fact}/{row.Aggregate}.{row.Method}/{row.Source}")) +
            Environment.NewLine + "Extra: " + string.Join(", ", extraPaths.Select(static row =>
                $"{row.Fact}/{row.Aggregate}.{row.Method}/{row.Source}")) +
            Environment.NewLine + "Invalid route evidence: " +
            string.Join(", ", invalidGapEvidence) + Environment.NewLine +
            "Ambiguous aggregate operations: " + string.Join(", ", invalidAggregateMethodEvidence));
    }

    [Fact]
    public void ShouldClassifyCommandsWithoutHandlersGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var requestRows = inventory.RootElement.GetProperty("requests").EnumerateArray().ToArray();
        var allowedClassifications = new HashSet<string>(StringComparer.Ordinal)
        {
            "command",
            "confirmed_command_gap",
            "deferred_command",
            "internal_executor_context",
            "internal_reaction",
            "query_or_reconstruction",
        };
        var requestTypes = ContractAssemblies().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && IsRequest(type))
            .ToDictionary(static type => type.Name, StringComparer.Ordinal);
        var handlerTypes = typeof(FindingAggregate).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract &&
                type.GetInterfaces().Any(static @interface => @interface.IsGenericType &&
                    @interface.GetGenericTypeDefinition().FullName is
                        "Cntryl.Portia.IRequestHandler`1" or "Cntryl.Portia.IRequestHandler`2"))
            .GroupBy(static type => type.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();
        _ = services.AddCompliance(configuration, developerAuthentication: true);
        var registeredHandlers = services.Where(static descriptor =>
                descriptor.ServiceType == typeof(RequestHandlerRegistration))
            .Select(static descriptor => (RequestHandlerRegistration)descriptor.ImplementationInstance!)
            .Select(static registration =>
                (Request: registration.RequestType.Name, Handler: registration.HandlerType.Name))
            .ToArray();

        // Act
        var failures = requestRows.Where(static request => request.GetProperty("classification").GetString() is
                "confirmed_command_gap" or "deferred_command")
            .Where(static request => string.IsNullOrWhiteSpace(request.GetProperty("note").GetString()) ||
                request.GetProperty("handlerRegistration").GetBoolean() ||
                request.GetProperty("handlers").GetArrayLength() != 0)
            .Select(static request => request.GetProperty("request").GetString()!).ToArray();
        var unknownClassifications = requestRows.Where(request =>
                !allowedClassifications.Contains(request.GetProperty("classification").GetString() ?? ""))
            .Select(static request => request.GetProperty("request").GetString()!).ToArray();
        var invalidHandlerPaths = requestRows.Where(static request =>
                request.GetProperty("classification").GetString() is "command" or "internal_reaction")
            .SelectMany(request =>
            {
                var requestName = request.GetProperty("request").GetString()!;
                if (!requestTypes.TryGetValue(requestName, out var requestType))
                    return [$"{requestName}: request type is missing"];

                var handlers = request.GetProperty("handlers").EnumerateArray().ToArray();
                if (handlers.Length == 0 || !request.GetProperty("handlerRegistration").GetBoolean())
                    return [$"{requestName}: no registered handler"];

                return handlers.Select(handler =>
                {
                    var handlerName = handler.GetProperty("name").GetString()!;
                    if (!handler.GetProperty("registered").GetBoolean() ||
                        !handlerTypes.TryGetValue(handlerName, out var candidates) ||
                        !candidates.Any(candidate => HandlesRequest(candidate, requestType)))
                        return $"{requestName}: invalid or unregistered handler {handlerName}";
                    return null;
                }).Where(static failure => failure is not null).Cast<string>();
            }).ToArray();
        var expectedRegistrations = requestRows.SelectMany(request =>
            request.GetProperty("handlers").EnumerateArray()
                .Where(static handler => handler.GetProperty("registered").GetBoolean())
                .Select(handler =>
                    (Request: request.GetProperty("request").GetString()!,
                        Handler: handler.GetProperty("name").GetString()!)))
            .ToArray();

        // Assert
        Assert.Empty(failures);
        Assert.Empty(unknownClassifications);
        Assert.Empty(invalidHandlerPaths);
        Assert.Equal(expectedRegistrations.OrderBy(static registration => registration.Request,
                StringComparer.Ordinal).ThenBy(static registration => registration.Handler,
                StringComparer.Ordinal),
            registeredHandlers.OrderBy(static registration => registration.Request,
                StringComparer.Ordinal).ThenBy(static registration => registration.Handler,
                StringComparer.Ordinal));
        Assert.Equal(registeredHandlers.Length, inventory.RootElement.GetProperty("counts")
            .GetProperty("registeredCoreHandlerRegistrations").GetInt32());
    }

    [Fact]
    public void ShouldInventoryApplicationMcpAdaptersAndTheirCoreRequestGivenCoverageMap()
    {
        // Arrange
        using var inventory = ReadInventory();
        var adapters = inventory.RootElement.GetProperty("applicationAdapters").EnumerateArray()
            .ToArray();
        var coreRequests = inventory.RootElement.GetProperty("requests").EnumerateArray()
            .Select(static request => request.GetProperty("request").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var assembly = typeof(ReviseApplicationMcpInput).Assembly;
        var actualAdapterRequests = assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract && IsRequest(type))
            .Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);
        var adapterTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(ReviseApplicationMcpInput)] = typeof(ReviseApplicationMcpInput),
            [nameof(PreviewApplicationChangeMcpInput)] = typeof(PreviewApplicationChangeMcpInput),
        };
        var repositoryRoot = FindRepositoryRoot();
        var handlerTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(ReviseApplicationMcpInputHandler)] = typeof(ReviseApplicationMcpInputHandler),
            [nameof(PreviewApplicationChangeMcpInputHandler)] = typeof(PreviewApplicationChangeMcpInputHandler),
        };
        var failures = new List<string>();

        // Act
        foreach (var adapter in adapters)
        {
            var requestName = adapter.GetProperty("request").GetString()!;
            var handlerName = adapter.GetProperty("handler").GetString()!;
            var forwardedRequest = adapter.GetProperty("forwardsTo").GetString()!;
            var handlerSource = Path.Combine(repositoryRoot,
                adapter.GetProperty("handlerSource").GetString()!);
            var registrationSource = Path.Combine(repositoryRoot,
                adapter.GetProperty("registrationSource").GetString()!);
            var handlerRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(handlerSource)).GetRoot();
            var registrationRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(registrationSource))
                .GetRoot();
            if (!adapterTypes.TryGetValue(requestName, out var requestType) ||
                requestType.Assembly != assembly)
                failures.Add($"{requestName}: app request type is missing");
            if (!handlerTypes.TryGetValue(handlerName, out var handlerType) ||
                handlerType.Assembly != assembly || requestType is null ||
                !HandlesRequest(handlerType, requestType))
                failures.Add($"{requestName}: app adapter handler does not handle the listed request");
            if (!coreRequests.Contains(forwardedRequest))
                failures.Add($"{requestName}: forwarded Core request {forwardedRequest} is missing");
            if (!HasGenericInvocation(registrationRoot, "AddRequestHandler", handlerName))
                failures.Add($"{requestName}: app handler {handlerName} is not registered");
            if (!DispatchesRequest(handlerRoot, forwardedRequest))
                failures.Add($"{requestName}: handler does not dispatch forwarded request {forwardedRequest}");
        }

        // Assert
        Assert.Equal(actualAdapterRequests.Order(StringComparer.Ordinal), adapterTypes.Keys
            .Order(StringComparer.Ordinal));
        Assert.Equal(adapterTypes.Keys.Order(StringComparer.Ordinal), adapters
            .Select(static adapter => adapter.GetProperty("request").GetString()!)
            .Order(StringComparer.Ordinal));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    static JsonDocument ReadInventory() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "aggregate-command-coverage.json")));

    static Assembly[] ContractAssemblies() =>
        [typeof(FindingRaisedV2).Assembly, typeof(FindingAggregate).Assembly];

    static bool IsRequest(Type type) => type.GetInterfaces().Any(static @interface =>
        @interface.FullName == "Cntryl.Portia.IRequest" ||
        @interface.IsGenericType &&
        @interface.GetGenericTypeDefinition().FullName == "Cntryl.Portia.IRequest`1");

    static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
        NullableTypeSyntax nullable => SimpleName(nullable.ElementType),
        _ => type.ToString().Split('.').Last().Split('<').First().TrimEnd('?'),
    };

    static bool HasGenericInvocation(SyntaxNode root, string method, string typeArgument) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
        {
            var generic = invocation.Expression switch
            {
                GenericNameSyntax name => name,
                MemberAccessExpressionSyntax member when member.Name is GenericNameSyntax name => name,
                _ => null,
            };
            return generic?.Identifier.ValueText == method && generic.TypeArgumentList.Arguments
                .Any(argument => SimpleName(argument) == typeArgument);
        });

    static bool DispatchesRequest(SyntaxNode root, string requestType)
    {
        var requestVariables = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                SimpleName(creation.Type) == requestType)
            .Select(static variable => variable.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        return root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                member.Name is not IdentifierNameSyntax name || name.Identifier.ValueText != "DispatchAsync")
                return false;

            var firstArgument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            return firstArgument is ObjectCreationExpressionSyntax creation &&
                       SimpleName(creation.Type) == requestType ||
                   firstArgument is IdentifierNameSyntax identifier &&
                       requestVariables.Contains(identifier.Identifier.ValueText);
        });
    }

    static string? InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
        _ => null,
    };

    static HashSet<string> AggregateFactConstructionNames(SyntaxNode method,
        HashSet<string> factNames, HashSet<string> publisherNames,
        IReadOnlyList<MethodDeclarationSyntax> siblingMethods)
    {
        var constructed = new HashSet<string>(StringComparer.Ordinal);
        var publisherCalls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(invocation => InvocationName(invocation) is { } name &&
                (publisherNames.Contains(name) || IsFactRaiseCallback(method, name, factNames)))
            .ToArray();
        foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var fact = SimpleName(creation.Type);
            if (factNames.Contains(fact) && (IsPublishedFactConstruction(creation, method,
                    publisherCalls) || IsPublishedThroughAggregateHelper(creation, method, fact,
                    factNames, publisherNames, siblingMethods)))
                constructed.Add(fact);
        }
        foreach (var creation in method.DescendantNodes()
                     .OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            var fact = TargetTypedFactName(creation, method, factNames, siblingMethods);
            if (fact is null)
                continue;
            if (factNames.Contains(fact) && (IsPublishedFactConstruction(creation, method,
                    publisherCalls) || IsPublishedThroughAggregateHelper(creation, method, fact,
                    factNames, publisherNames, siblingMethods)))
                constructed.Add(fact);
        }
        return constructed;
    }

    static string? TargetTypedFactName(ImplicitObjectCreationExpressionSyntax creation,
        SyntaxNode method, HashSet<string> factNames,
        IReadOnlyList<MethodDeclarationSyntax> siblingMethods)
    {
        var argument = creation.Ancestors().OfType<ArgumentSyntax>()
            .FirstOrDefault(candidate => candidate.Expression.Span.Contains(creation.Span));
        var invocation = argument?.Parent?.Parent as InvocationExpressionSyntax;
        if (argument is null || invocation is null)
        {
            var declaration = creation.Ancestors().OfType<VariableDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Variables.Any(variable =>
                    variable.Initializer?.Value == creation));
            return declaration is null ? null : SimpleName(declaration.Type);
        }

        var methodName = InvocationName(invocation);
        if (methodName is null)
            return null;
        if (FactRaiseCallbackType(method, methodName, factNames) is { } callbackFact)
            return callbackFact;

        var argumentIndex = invocation.ArgumentList.Arguments.IndexOf(argument);
        var candidates = siblingMethods.Where(candidate =>
                candidate.Identifier.ValueText == methodName &&
                AcceptsArgumentCount(candidate, invocation.ArgumentList.Arguments.Count) &&
                argumentIndex < candidate.ParameterList.Parameters.Count)
            .Select(candidate => candidate.ParameterList.Parameters[argumentIndex].Type is { } type
                ? SimpleName(type)
                : null)
            .Where(candidate => candidate is not null && factNames.Contains(candidate))
            .Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    static Dictionary<string, HashSet<string>> AggregateFactPublisherNamesByType(
        Dictionary<string, SyntaxNode> sourceRoots, HashSet<string> relevantTypes) =>
        sourceRoots.Values.SelectMany(static root =>
                root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(type => relevantTypes.Contains(type.Identifier.ValueText))
            .GroupBy(static type => type.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key,
                group => AggregateFactPublisherNames(group), StringComparer.Ordinal);

    static Dictionary<string, MethodDeclarationSyntax[]> AggregateMethodsByType(
        Dictionary<string, SyntaxNode> sourceRoots, HashSet<string> relevantTypes) =>
        sourceRoots.Values.SelectMany(static root =>
                root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(type => relevantTypes.Contains(type.Identifier.ValueText))
            .GroupBy(static type => type.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key,
                static group => group.SelectMany(type =>
                    type.Members.OfType<MethodDeclarationSyntax>()).ToArray(),
                StringComparer.Ordinal);

    static HashSet<string> AggregateFactPublisherNames(
        IEnumerable<ClassDeclarationSyntax> aggregateParts)
    {
        var publisherNames = new HashSet<string>(["RaiseEvent", "RaiseAuditEvent", "AuditEvent"],
            StringComparer.Ordinal);
        var methods = aggregateParts.SelectMany(static part =>
            part.Members.OfType<MethodDeclarationSyntax>()).ToArray();
        bool changed;
        do
        {
            changed = false;
            foreach (var method in methods)
                if (!publisherNames.Contains(method.Identifier.ValueText) &&
                    method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
                        InvocationName(invocation) is { } name && publisherNames.Contains(name)))
                    changed |= publisherNames.Add(method.Identifier.ValueText);
        } while (changed);

        return publisherNames;
    }

    static bool IsFactRaiseCallback(SyntaxNode method, string name, HashSet<string> factNames) =>
        FactRaiseCallbackType(method, name, factNames) is not null;

    static string? FactRaiseCallbackType(SyntaxNode method, string name,
        HashSet<string> factNames)
    {
        if (method is not MethodDeclarationSyntax declaration)
            return null;
        var parameter = declaration.ParameterList.Parameters.FirstOrDefault(candidate =>
            candidate.Identifier.ValueText == name);
        if (parameter?.Type is not { } type)
            return null;
        if (type is NullableTypeSyntax nullable)
            type = nullable.ElementType;
        var action = type.DescendantNodesAndSelf().OfType<GenericNameSyntax>()
            .FirstOrDefault(static generic => generic.Identifier.ValueText == "Action");
        if (action?.TypeArgumentList.Arguments.Count != 1)
            return null;
        var fact = SimpleName(action.TypeArgumentList.Arguments[0]);
        return factNames.Contains(fact) ? fact : null;
    }

    static bool IsPublishedFactConstruction(SyntaxNode creation, SyntaxNode method,
        IReadOnlyList<InvocationExpressionSyntax> publisherCalls)
    {
        if (creation.Ancestors().OfType<BinaryExpressionSyntax>().Any(binary =>
                (binary.IsKind(SyntaxKind.EqualsExpression) ||
                 binary.IsKind(SyntaxKind.NotEqualsExpression)) &&
                binary.Span.Contains(creation.Span)))
            return false;

        if (publisherCalls.Any(call => call.ArgumentList.Arguments.Any(argument =>
                argument.Expression.Span.Contains(creation.Span))))
            return true;

        var variable = FactConstructionVariableName(creation);
        if (variable is null)
            return false;
        if (publisherCalls.Any(call => call.ArgumentList.Arguments.Any(argument =>
                argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Any(identifier => identifier.Identifier.ValueText == variable))))
            return true;

        return method.DescendantNodes().OfType<ForEachStatementSyntax>().Any(loop =>
            loop.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.ValueText == variable) &&
            publisherCalls.Any(call => loop.Statement.Span.Contains(call.Span) &&
                call.ArgumentList.Arguments.Any(argument =>
                    argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                        .Any(identifier => identifier.Identifier.ValueText ==
                            loop.Identifier.ValueText))));
    }

    static bool IsPublishedThroughAggregateHelper(SyntaxNode creation, SyntaxNode method,
        string fact, HashSet<string> factNames, HashSet<string> publisherNames,
        IReadOnlyList<MethodDeclarationSyntax> siblingMethods)
    {
        if (method is not MethodDeclarationSyntax helper ||
            !helper.ReturnType.DescendantNodesAndSelf().OfType<TypeSyntax>()
                .Any(type => SimpleName(type) == fact) ||
            !FactConstructionIsReturned(creation, helper))
            return false;

        foreach (var caller in siblingMethods)
        {
            var publisherCalls = caller.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => InvocationName(invocation) is { } name &&
                    (publisherNames.Contains(name) || IsFactRaiseCallback(caller, name, factNames)))
                .ToArray();
            var helperCalls = caller.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => InvocationName(invocation) == helper.Identifier.ValueText &&
                    AcceptsArgumentCount(helper, invocation.ArgumentList.Arguments.Count))
                .ToArray();
            if (helperCalls.Any(helperCall => publisherCalls.Any(publisherCall =>
                    publisherCall.ArgumentList.Arguments.Any(argument =>
                        argument.Expression.Span.Contains(helperCall.Span)))))
                return true;

            foreach (var helperCall in helperCalls)
            {
                var resultVariable = caller.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(variable => variable.Initializer?.Value.Span
                        .Contains(helperCall.Span) == true)?.Identifier.ValueText;
                if (resultVariable is null)
                {
                    if (caller.DescendantNodes().OfType<ForEachStatementSyntax>().Any(loop =>
                            loop.Expression.Span.Contains(helperCall.Span) &&
                            publisherCalls.Any(publisherCall =>
                                loop.Statement.Span.Contains(publisherCall.Span) &&
                                PublisherUsesVariable(publisherCall, loop.Identifier.ValueText))))
                        return true;
                    continue;
                }

                if (publisherCalls.Any(publisherCall =>
                        PublisherUsesVariable(publisherCall, resultVariable)))
                    return true;
                if (caller.DescendantNodes().OfType<ForEachStatementSyntax>().Any(loop =>
                        loop.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                            .Any(identifier => identifier.Identifier.ValueText == resultVariable) &&
                        publisherCalls.Any(publisherCall =>
                            loop.Statement.Span.Contains(publisherCall.Span) &&
                            PublisherUsesVariable(publisherCall, loop.Identifier.ValueText))))
                    return true;
            }
        }
        return false;
    }

    static string? FactConstructionVariableName(SyntaxNode creation)
    {
        var variable = creation.Ancestors().OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Initializer?.Value.Span.Contains(creation.Span) == true)
            ?.Identifier.ValueText;
        return variable ?? (creation.Ancestors().OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(candidate => candidate.Right.Span.Contains(creation.Span))?.Left
            is IdentifierNameSyntax assigned ? assigned.Identifier.ValueText : null);
    }

    static bool FactConstructionIsReturned(SyntaxNode creation, MethodDeclarationSyntax method)
    {
        var returnExpressions = method.DescendantNodes().OfType<ReturnStatementSyntax>()
            .Where(static statement => statement.Expression is not null)
            .Select(static statement => statement.Expression!)
            .Concat(method.ExpressionBody is { } body ? [body.Expression] : [])
            .ToArray();
        if (returnExpressions.Any(expression => expression.Span.Contains(creation.Span)))
            return true;

        var factVariable = FactConstructionVariableName(creation);
        if (factVariable is null)
            return false;
        if (returnExpressions.Any(expression => expression.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>().Any(identifier =>
                    identifier.Identifier.ValueText == factVariable)))
            return true;

        var returnedCollections = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(invocation => InvocationName(invocation) == "Add" &&
                invocation.ArgumentList.Arguments.Any(argument => argument.Expression
                    .DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier =>
                        identifier.Identifier.ValueText == factVariable)))
            .Select(static invocation => invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Expression is IdentifierNameSyntax collection
                    ? collection.Identifier.ValueText : null)
            .Where(static name => name is not null).ToHashSet(StringComparer.Ordinal);
        return returnExpressions.Any(expression => expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>().Any(identifier =>
                returnedCollections.Contains(identifier.Identifier.ValueText)));
    }

    static bool PublisherUsesVariable(InvocationExpressionSyntax publisher, string variable) =>
        publisher.ArgumentList.Arguments.Any(argument =>
            argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.ValueText == variable));

    static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Compliance.Core")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not find the Compliance Core repository root.");
    }

    static bool HandlesRequest(Type handlerType, Type requestType) => handlerType.GetInterfaces()
        .Where(static @interface => @interface.IsGenericType &&
            @interface.GetGenericTypeDefinition().FullName is
                "Cntryl.Portia.IRequestHandler`1" or "Cntryl.Portia.IRequestHandler`2")
        .Any(@interface => @interface.GetGenericArguments()[0] == requestType);

    static Dictionary<string, SyntaxNode> ReadSourceRoots()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs",
                SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
                path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot(),
                StringComparer.Ordinal);
    }

    static Dictionary<string, string[]> GetTypeSources(Dictionary<string, SyntaxNode> sourceRoots) =>
        sourceRoots.SelectMany(pair => pair.Value.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Select(type => (Name: type.Identifier.ValueText, pair.Key)))
            .GroupBy(static type => type.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key,
                static group => group.Select(static type => type.Key).Distinct(StringComparer.Ordinal)
                    .ToArray(), StringComparer.Ordinal);

    static HashSet<(string Aggregate, string Method, int ArgumentCount)> AggregateMutationOperationsForRequest(
        JsonElement request, Dictionary<string, SyntaxNode> sourceRoots,
        Dictionary<string, string[]> typeSources, HashSet<string> aggregateNames)
    {
        var handlers = new List<(TypeDeclarationSyntax Type, SyntaxNode Root)>();
        foreach (var handler in request.GetProperty("handlers").EnumerateArray()
                     .Where(static item => item.GetProperty("registered").GetBoolean()))
        {
            var source = handler.GetProperty("source").GetString()!;
            var name = handler.GetProperty("name").GetString()!;
            if (sourceRoots.TryGetValue(source, out var handlerRoot))
                handlers.AddRange(handlerRoot.DescendantNodes().OfType<TypeDeclarationSyntax>()
                    .Where(type => type.Identifier.ValueText == name)
                    .Select(type => (type, handlerRoot)));
        }

        var requestMethods = new List<(MethodDeclarationSyntax Method, SyntaxNode Root)>();
        foreach (var (handlerType, handlerRoot) in handlers)
            requestMethods.AddRange(handlerType.Members.OfType<MethodDeclarationSyntax>()
                .Select(method => (method, handlerRoot)));

        var routeMethods = new List<(MethodDeclarationSyntax Method, SyntaxNode Root)>(requestMethods);
        var handlerCalls = requestMethods.SelectMany(static item =>
                item.Method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .ToArray();
        foreach (var helperName in request.GetProperty("helperServices").EnumerateArray()
                     .Select(static helper => helper.GetString()!))
        {
            foreach (var source in typeSources.GetValueOrDefault(helperName) ?? [])
            {
                if (!sourceRoots.TryGetValue(source, out var helperRoot))
                    continue;
                foreach (var helperType in helperRoot.DescendantNodes()
                             .OfType<TypeDeclarationSyntax>()
                             .Where(type => type.Identifier.ValueText == helperName))
                {
                    var entryPoints = MethodsCalledBy(helperType, handlerCalls).ToArray();
                    var reachableMethods = ReachableMethods(helperType, entryPoints);
                    routeMethods.AddRange(reachableMethods.Select(method => (method, helperRoot)));
                }
            }
        }

        var operations = AggregateMutationOperations(routeMethods, aggregateNames);
        return operations;
    }

    static List<MethodDeclarationSyntax> ReachableMethods(TypeDeclarationSyntax type,
        IEnumerable<MethodDeclarationSyntax> entryPoints)
    {
        var methods = new List<MethodDeclarationSyntax>();
        var visited = new HashSet<MethodDeclarationSyntax>();
        var pending = new Queue<MethodDeclarationSyntax>(entryPoints);
        while (pending.TryDequeue(out var method))
        {
            if (!visited.Add(method))
                continue;
            methods.Add(method);
            foreach (var called in MethodsCalledBy(type,
                         method.DescendantNodes().OfType<InvocationExpressionSyntax>()))
                pending.Enqueue(called);
        }
        return methods;
    }

    static IEnumerable<MethodDeclarationSyntax> MethodsCalledBy(TypeDeclarationSyntax type,
        IEnumerable<InvocationExpressionSyntax> calls)
    {
        var methods = type.Members.OfType<MethodDeclarationSyntax>().ToArray();
        foreach (var call in calls)
        {
            var name = InvocationName(call);
            if (name is null)
                continue;
            var argumentCount = call.ArgumentList.Arguments.Count;
            foreach (var method in methods.Where(method => method.Identifier.ValueText == name &&
                         AcceptsArgumentCount(method, argumentCount)))
                yield return method;
        }
    }

    static bool AcceptsArgumentCount(MethodDeclarationSyntax method, int argumentCount)
    {
        if (argumentCount > method.ParameterList.Parameters.Count)
            return method.ParameterList.Parameters.LastOrDefault()?.Modifiers.Any(static modifier =>
                modifier.IsKind(SyntaxKind.ParamsKeyword)) == true;
        return method.ParameterList.Parameters.Skip(argumentCount)
            .All(static parameter => parameter.Default is not null ||
                parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ParamsKeyword)));
    }

    static HashSet<(string Aggregate, string Method, int ArgumentCount)> AggregateMutationOperations(
        IEnumerable<(MethodDeclarationSyntax Method, SyntaxNode Root)> methods,
        HashSet<string> aggregateNames)
    {
        var operations = new HashSet<(string Aggregate, string Method, int ArgumentCount)>();
        var methodDeclarations = methods.Select(static item => item.Method).ToArray();
        foreach (var (method, root) in methods)
            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                    member.Expression is not IdentifierNameSyntax receiver)
                    continue;
                var lambda = invocation.Ancestors().OfType<LambdaExpressionSyntax>()
                    .FirstOrDefault(candidate => LambdaParameters(candidate).Any(parameter =>
                        parameter.Identifier.ValueText == receiver.Identifier.ValueText));
                var lambdaParameterIndex = lambda is null ? -1 : Array.FindIndex(
                    LambdaParameters(lambda), parameter =>
                        parameter.Identifier.ValueText == receiver.Identifier.ValueText);
                var aggregate = lambda is not null
                    ? ResolveAggregateLambdaType(lambda, lambdaParameterIndex, root,
                        methodDeclarations, aggregateNames)
                    : ResolveAggregateVariableType(receiver.Identifier.ValueText, method, root,
                        methodDeclarations, aggregateNames);
                if (aggregate is null)
                    continue;
                operations.Add((aggregate, member.Name.Identifier.ValueText,
                    invocation.ArgumentList.Arguments.Count));
            }
        return operations;
    }

    static HashSet<(string Aggregate, string Method, int ArgumentCount, string Source)> FindAggregateOperationCalls(
        Dictionary<string, SyntaxNode> sourceRoots, HashSet<string> aggregateNames)
    {
        var calls = new HashSet<(string Aggregate, string Method, int ArgumentCount, string Source)>();
        foreach (var (source, root) in sourceRoots)
        {
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Select(method => (Method: method, Root: root));
            foreach (var operation in AggregateMutationOperations(methods, aggregateNames))
                calls.Add((operation.Aggregate, operation.Method, operation.ArgumentCount, source));
        }
        return calls;
    }

    static string? ResolveAggregateLambdaType(LambdaExpressionSyntax lambda, int parameterIndex,
        SyntaxNode root, MethodDeclarationSyntax[] localMethods, HashSet<string> aggregateNames)
    {
        var parameter = LambdaParameters(lambda)[parameterIndex];
        if (parameter.Type is not null && TryResolveAggregateName(parameter.Type, root,
                aggregateNames) is { } explicitType)
            return explicitType;

        var argument = lambda.Ancestors().OfType<ArgumentSyntax>()
            .FirstOrDefault(candidate => candidate.Expression.Span.Contains(lambda.Span));
        var invocation = argument?.Parent?.Parent as InvocationExpressionSyntax;
        if (argument is null || invocation is null)
            return null;
        var argumentIndex = argument.Parent!.ChildNodes().OfType<ArgumentSyntax>()
            .TakeWhile(candidate => !ReferenceEquals(candidate, argument)).Count();
        var invocationName = InvocationName(invocation);
        if (invocationName == "ExecuteAsync")
        {
            var firstArgument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            var containingMethod = lambda.Ancestors().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
            if (ResolveAggregateTypeFromExpression(firstArgument, containingMethod, root,
                    localMethods, aggregateNames) is { } directExecutorType)
                return directExecutorType;
        }

        var candidates = localMethods.Where(method => method.Identifier.ValueText == invocationName &&
            AcceptsArgumentCount(method, invocation.ArgumentList.Arguments.Count) &&
            argumentIndex < method.ParameterList.Parameters.Count)
            .Select(method => method.ParameterList.Parameters[argumentIndex].Type is { } parameterType
                ? TryResolveAggregateName(parameterType, root, aggregateNames)
                : null).Where(static name => name is not null).Distinct(StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 1)
            return candidates[0];
        return null;
    }

    static string? ResolveAggregateTypeFromExpression(ExpressionSyntax? expression,
        MethodDeclarationSyntax? containingMethod, SyntaxNode root,
        MethodDeclarationSyntax[] localMethods, HashSet<string> aggregateNames)
    {
        switch (expression)
        {
            case ObjectCreationExpressionSyntax creation:
                return TryResolveAggregateName(creation.Type, root, aggregateNames);
            case ImplicitObjectCreationExpressionSyntax creation:
                return TryResolveAggregateNameFromContext(creation, root, aggregateNames);
            case ParenthesizedExpressionSyntax parenthesized:
                return ResolveAggregateTypeFromExpression(parenthesized.Expression, containingMethod,
                    root, localMethods, aggregateNames);
            case AwaitExpressionSyntax awaited:
                return ResolveAggregateTypeFromExpression(awaited.Expression, containingMethod,
                    root, localMethods, aggregateNames);
            case IdentifierNameSyntax identifier when containingMethod is not null:
                {
                    var variable = containingMethod.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                        .FirstOrDefault(candidate => candidate.Identifier.ValueText == identifier.Identifier.ValueText);
                    return ResolveAggregateTypeFromExpression(variable?.Initializer?.Value,
                            containingMethod, root, localMethods, aggregateNames);
                }
            case InvocationExpressionSyntax called:
                {
                    if (called.Expression is MemberAccessExpressionSyntax
                        { Name.Identifier.ValueText: "ConfigureAwait" } configureAwait)
                        return ResolveAggregateTypeFromExpression(configureAwait.Expression,
                            containingMethod, root, localMethods, aggregateNames);
                    var name = InvocationName(called);
                    if (name == "HydrateAsync")
                    {
                        var hydratedType = called.ArgumentList.Arguments
                            .SelectMany(static argument => argument.Expression.DescendantNodesAndSelf()
                                .OfType<ObjectCreationExpressionSyntax>())
                            .Select(creation => TryResolveAggregateName(creation.Type, root,
                                aggregateNames))
                            .FirstOrDefault(static candidate => candidate is not null);
                        if (hydratedType is not null)
                            return hydratedType;
                    }
                    var candidates = localMethods.Where(method => method.Identifier.ValueText == name &&
                            AcceptsArgumentCount(method, called.ArgumentList.Arguments.Count))
                        .Select(method => TryResolveAggregateName(method.ReturnType, method.SyntaxTree.GetRoot(),
                            aggregateNames)).Where(static candidate => candidate is not null)
                        .Distinct(StringComparer.Ordinal).ToArray();
                    return candidates.Length == 1 ? candidates[0] : null;
                }
            default:
                return null;
        }
    }

    static string? ResolveAggregateVariableType(string variableName,
        MethodDeclarationSyntax containingMethod, SyntaxNode root,
        MethodDeclarationSyntax[] localMethods, HashSet<string> aggregateNames)
    {
        var variable = containingMethod.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.ValueText == variableName);
        return ResolveAggregateTypeFromExpression(variable?.Initializer?.Value,
            containingMethod, root, localMethods, aggregateNames);
    }

    static string? TryResolveAggregateNameFromContext(ImplicitObjectCreationExpressionSyntax creation,
        SyntaxNode root, HashSet<string> aggregateNames)
    {
        var declaration = creation.Ancestors().OfType<VariableDeclarationSyntax>().FirstOrDefault();
        return declaration is null ? null : TryResolveAggregateName(declaration.Type, root, aggregateNames);
    }

    static string? TryResolveAggregateName(TypeSyntax type, SyntaxNode root,
        HashSet<string> aggregateNames)
    {
        var aliases = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(static directive => directive.Alias is not null)
            .ToDictionary(static directive => directive.Alias!.Name.Identifier.ValueText,
                static directive => directive.Name is { } name ? SimpleName(name) : "",
                StringComparer.Ordinal);
        foreach (var candidate in type.DescendantNodesAndSelf().OfType<TypeSyntax>())
        {
            var name = SimpleName(candidate);
            if (aliases.TryGetValue(name, out var target))
                name = target;
            if (aggregateNames.Contains(name))
                return name;
        }
        return null;
    }

    static ParameterSyntax[] LambdaParameters(LambdaExpressionSyntax lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax simple => [simple.Parameter],
        ParenthesizedLambdaExpressionSyntax parenthesized =>
            parenthesized.ParameterList.Parameters.ToArray(),
        _ => [],
    };

    static bool IsCallableAggregateMethod(MethodDeclarationSyntax method) =>
        method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword) ||
            modifier.IsKind(SyntaxKind.InternalKeyword) ||
            modifier.IsKind(SyntaxKind.ProtectedKeyword));

    static bool IsForwardingOverload(MethodDeclarationSyntax method,
        MethodDeclarationSyntax[] aggregateMethods)
    {
        var expression = method.ExpressionBody?.Expression;
        var forwardedCall = expression is InvocationExpressionSyntax expressionInvocation &&
            InvocationName(expressionInvocation) == method.Identifier.ValueText
                ? expressionInvocation
                : method.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(invocation => InvocationName(invocation) == method.Identifier.ValueText)
                    .SingleOrDefault(invocation => method.Body.DescendantNodes()
                        .OfType<ReturnStatementSyntax>().Any(statement =>
                            statement.Expression == invocation));
        return forwardedCall is not null && aggregateMethods.Any(candidate =>
            candidate != method && candidate.Identifier.ValueText == method.Identifier.ValueText &&
            IsCallableAggregateMethod(candidate) && AcceptsArgumentCount(candidate,
                forwardedCall.ArgumentList.Arguments.Count));
    }

    static Dictionary<string, List<AggregateMethodInfo>> ReadAggregateMethods(
        Dictionary<string, SyntaxNode> sourceRoots, HashSet<string> aggregateNames)
    {
        var aggregateMethods = new Dictionary<string, List<AggregateMethodInfo>>(StringComparer.Ordinal);
        foreach (var (source, syntaxRoot) in sourceRoots)
            foreach (var aggregate in syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(type => aggregateNames.Contains(type.Identifier.ValueText)))
            {
                var aggregateName = aggregate.Identifier.ValueText;
                if (!aggregateMethods.TryGetValue(aggregateName, out var methods))
                    aggregateMethods.Add(aggregateName, methods = []);
                var declarations = aggregate.Members.OfType<MethodDeclarationSyntax>().ToArray();
                foreach (var method in declarations)
                {
                    var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                        .Where(static invocation => InvocationName(invocation) is not null)
                        .Select(static invocation => (Method: InvocationName(invocation)!,
                            ArgumentCount: invocation.ArgumentList.Arguments.Count))
                        .ToHashSet();
                    methods.Add(new AggregateMethodInfo(method.Identifier.ValueText, source,
                        IsCallableAggregateMethod(method), IsForwardingOverload(method, declarations),
                        method.ParameterList.Parameters.Count,
                        method.ParameterList.Parameters.Count(parameter => parameter.Default is null &&
                            !parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ParamsKeyword))),
                        method.ParameterList.Parameters.LastOrDefault()?.Modifiers.Any(static modifier =>
                            modifier.IsKind(SyntaxKind.ParamsKeyword)) == true, calls));
                }
            }
        return aggregateMethods;
    }

    static HashSet<(string Fact, string Aggregate, string Method, int MethodArgumentCount, string Source)>
        FindAggregateFactProducerOperations(JsonElement root,
            Dictionary<string, SyntaxNode> sourceRoots, HashSet<string> aggregateNames,
            Dictionary<string, List<AggregateMethodInfo>> aggregateMethods)
    {
        var aggregateFactNames = root.GetProperty("facts").EnumerateArray()
            .Where(static fact => fact.GetProperty("reducers").GetArrayLength() > 0)
            .Select(static fact => fact.GetProperty("fact").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var helperProducerFacts = new List<(string Method, string RouteThroughMethod, string Fact)>();
        foreach (var helper in root.GetProperty("factEmissionHelpers").EnumerateArray())
        {
            var helperName = helper.GetProperty("name").GetString()!;
            var helperSource = helper.GetProperty("source").GetString()!;
            var routeThroughMethod = helper.GetProperty("routeThroughMethod").GetString()!;
            if (!sourceRoots.TryGetValue(helperSource, out var helperRoot))
                continue;
            var helperPublisherNamesByType = AggregateFactPublisherNamesByType(sourceRoots,
                [helperName]);
            var helperMethodsByType = AggregateMethodsByType(sourceRoots, [helperName]);
            foreach (var helperType in helperRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(type => type.Identifier.ValueText == helperName))
            {
                var publisherNames = helperPublisherNamesByType[helperName];
                foreach (var method in helperType.Members.OfType<MethodDeclarationSyntax>())
                    foreach (var fact in AggregateFactConstructionNames(method, aggregateFactNames,
                                 publisherNames, helperMethodsByType[helperName]))
                        helperProducerFacts.Add((method.Identifier.ValueText, routeThroughMethod, fact));
            }
        }

        var emitters = new HashSet<(string Fact, string Aggregate, string Method,
            int MethodArgumentCount, string Source)>();
        var helperNames = root.GetProperty("factEmissionHelpers").EnumerateArray()
            .Select(static helper => helper.GetProperty("name").GetString()!);
        var relevantTypes = aggregateNames.Concat(helperNames).ToHashSet(StringComparer.Ordinal);
        var publisherNamesByType = AggregateFactPublisherNamesByType(sourceRoots, relevantTypes);
        var methodsByType = AggregateMethodsByType(sourceRoots, relevantTypes);
        foreach (var (source, syntaxRoot) in sourceRoots)
            foreach (var aggregate in syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(type => aggregateNames.Contains(type.Identifier.ValueText)))
            {
                var publisherNames = publisherNamesByType[aggregate.Identifier.ValueText];
                foreach (var method in aggregate.Members.OfType<MethodDeclarationSyntax>())
                {
                    var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                        .Where(static invocation => InvocationName(invocation) is not null)
                        .Select(static invocation => (Method: InvocationName(invocation)!,
                            ArgumentCount: invocation.ArgumentList.Arguments.Count))
                        .ToHashSet();
                    var aggregateName = aggregate.Identifier.ValueText;
                    foreach (var fact in AggregateFactConstructionNames(method, aggregateFactNames,
                                 publisherNames, methodsByType[aggregate.Identifier.ValueText]))
                        emitters.Add((fact, aggregateName, method.Identifier.ValueText,
                            method.ParameterList.Parameters.Count, source));
                    foreach (var helperProducer in helperProducerFacts.Where(helper =>
                                 calls.Any(call => call.Method == helper.Method) &&
                                 calls.Any(call => call.Method == helper.RouteThroughMethod)))
                        emitters.Add((helperProducer.Fact, aggregateName, method.Identifier.ValueText,
                            method.ParameterList.Parameters.Count, source));
                }
            }

        var producers = new HashSet<(string Fact, string Aggregate, string Method,
            int MethodArgumentCount, string Source)>();
        foreach (var emitter in emitters)
        {
            if (!aggregateMethods.TryGetValue(emitter.Aggregate, out var methods))
                continue;
            foreach (var operation in methods.Where(static method => method.IsCallable)
                         .Where(static method => !method.IsForwardingOverload)
                         .Where(operation => AggregateMethodCanReach(operation.Name,
                             operation.ParameterCount, emitter.Method, emitter.MethodArgumentCount,
                             methods)))
                producers.Add((emitter.Fact, emitter.Aggregate, operation.Name,
                    operation.ParameterCount, operation.Source));
        }
        return producers;
    }

    static bool AggregateMethodCanReach(string start, int startArgumentCount, string target,
        int targetParameterCount,
        List<AggregateMethodInfo> methods)
    {
        var startMethods = MatchingAggregateMethods(start, startArgumentCount, methods);
        var pending = new Queue<AggregateMethodInfo>(startMethods);
        var visited = new HashSet<AggregateMethodInfo>();
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current))
                continue;
            if (current.Name == target && current.ParameterCount == targetParameterCount)
                return true;
            foreach (var call in current.Calls)
                foreach (var next in MatchingAggregateMethods(call.Method, call.ArgumentCount, methods))
                    pending.Enqueue(next);
        }
        return false;
    }

    static int ReadMethodArgumentCount(JsonElement producer,
        Dictionary<string, List<AggregateMethodInfo>> aggregateMethods)
    {
        if (producer.TryGetProperty("methodArgumentCount", out var argumentCount))
            return argumentCount.GetInt32();
        var aggregate = producer.GetProperty("aggregate").GetString()!;
        var method = producer.GetProperty("method").GetString()!;
        return aggregateMethods[aggregate].Single(candidate => candidate.Name == method)
            .ParameterCount;
    }

    static AggregateMethodInfo[] MatchingAggregateMethods(string name, int argumentCount,
        List<AggregateMethodInfo> methods)
    {
        var candidates = methods.Where(method => method.Name == name &&
            method.RequiredParameterCount <= argumentCount &&
            (method.HasParams || argumentCount <= method.ParameterCount)).ToArray();
        if (candidates.Length <= 1)
            return candidates;
        var minimumParameterCount = candidates.Min(static method => method.ParameterCount);
        return candidates.Where(method => method.ParameterCount == minimumParameterCount).ToArray();
    }

    static void CheckRoutes(JsonElement aggregate, string requestProperty, string classification,
        string aggregateName, Dictionary<string, JsonElement> requests,
        Dictionary<string, SyntaxNode> sourceRoots, Dictionary<string, string[]> typeSources,
        HashSet<string> aggregateNames,
        Dictionary<string, List<AggregateMethodInfo>> aggregateMethods,
        HashSet<(string Fact, string Aggregate, string Method, int MethodArgumentCount, string Source)> producerOperations,
        List<string> failures)
    {
        foreach (var requestName in aggregate.GetProperty(requestProperty).EnumerateArray()
                     .Select(static item => item.GetString()!))
        {
            if (!requests.TryGetValue(requestName, out var request) ||
                request.GetProperty("classification").GetString() != classification ||
                !AggregateMutationOperationsForRequest(request, sourceRoots, typeSources,
                    aggregateNames).Any(operation => producerOperations.Any(producer =>
                    producer.Aggregate == operation.Aggregate &&
                    AggregateMethodCanReach(operation.Method, operation.ArgumentCount,
                        producer.Method, producer.MethodArgumentCount,
                        aggregateMethods[producer.Aggregate]) && operation.Aggregate == aggregateName)))
                failures.Add($"{aggregateName}: {requestName} has no registered mutation route classified as {classification}");
        }
    }

    sealed record AggregateMethodInfo(string Name, string Source, bool IsCallable,
        bool IsForwardingOverload,
        int ParameterCount, int RequiredParameterCount, bool HasParams,
        HashSet<(string Method, int ArgumentCount)> Calls);
}
