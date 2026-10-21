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
        var actual = typeof(RemediationLedger).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract &&
                typeof(Aggregate).IsAssignableFrom(type))
            .Select(static type => type.Name).ToHashSet(StringComparer.Ordinal);
        var requests = root.GetProperty("requests").EnumerateArray().ToArray();

        // Act
        var missingPaths = expected.Where(aggregate => !requests.Any(request =>
            request.GetProperty("classification").GetString() is "command" or "internal_reaction" &&
            request.GetProperty("aggregateTargets").EnumerateArray().Any(target =>
                target.GetString() == aggregate) &&
            request.GetProperty("handlerRegistration").GetBoolean() &&
            request.GetProperty("handlers").EnumerateArray().Any(static handler =>
                handler.GetProperty("registered").GetBoolean()))).ToArray();

        // Assert
        Assert.Equal(actual.Order(StringComparer.Ordinal), expected.Order(StringComparer.Ordinal));
        Assert.Empty(missingPaths);
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
        var failures = new List<string>();

        // Act
        foreach (var aggregate in aggregates.Values)
        {
            var aggregateName = aggregate.GetProperty("aggregate").GetString()!;
            CheckRoutes(aggregate, "commandRequests", "command", aggregateName, requests, failures);
            CheckRoutes(aggregate, "internalReactionRequests", "internal_reaction", aggregateName,
                requests, failures);
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
            foreach (var target in request.GetProperty("aggregateTargets").EnumerateArray()
                         .Select(static item => item.GetString()!))
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
        var facts = root.GetProperty("facts").EnumerateArray().ToArray();
        var factNames = facts.Select(static fact => fact.GetProperty("fact").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var repositoryRoot = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs",
            SearchOption.AllDirectories).ToArray();
        var actualReducers = new HashSet<(string Fact, string Aggregate, string Source)>();
        var actualEmitters = new HashSet<(string Fact, string Source)>();

        // Act
        foreach (var sourcePath in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, sourcePath).Replace('\\', '/');
            var syntaxRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath), path: relativePath)
                .GetRoot();
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
            foreach (var creation in syntaxRoot.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var fact = SimpleName(creation.Type);
                if (factNames.Contains(fact))
                    actualEmitters.Add((fact, relativePath));
            }
            foreach (var variable in syntaxRoot.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Initializer?.Value is not ImplicitObjectCreationExpressionSyntax)
                    continue;
                var declaration = variable.Ancestors().OfType<VariableDeclarationSyntax>()
                    .FirstOrDefault();
                if (declaration is null)
                    continue;
                var fact = SimpleName(declaration.Type);
                if (factNames.Contains(fact))
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
        var handlerTypes = typeof(RemediationLedger).Assembly.GetTypes()
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
            .ToHashSet();

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
            .ToHashSet();

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
        [typeof(FindingRaised).Assembly, typeof(RemediationLedger).Assembly];

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

    static void CheckRoutes(JsonElement aggregate, string requestProperty, string classification,
        string aggregateName, Dictionary<string, JsonElement> requests, List<string> failures)
    {
        foreach (var requestName in aggregate.GetProperty(requestProperty).EnumerateArray()
                     .Select(static item => item.GetString()!))
        {
            if (!requests.TryGetValue(requestName, out var request) ||
                request.GetProperty("classification").GetString() != classification ||
                !request.GetProperty("aggregateTargets").EnumerateArray()
                    .Any(target => target.GetString() == aggregateName))
                failures.Add($"{aggregateName}: {requestName} is not classified and targeted as {classification}");
        }
    }
}
