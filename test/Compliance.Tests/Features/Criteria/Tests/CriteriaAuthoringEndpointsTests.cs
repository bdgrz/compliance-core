using System.Text.Json;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Cntryl.Portia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaAuthoringEndpointsTests
{
    [Fact]
    public async Task ShouldMapDistinctAuthenticatedOperatorRoutesGivenOptIn()
    {
        // Arrange
        await using var app = WebApplication.CreateSlimBuilder().Build();
        (string Method, string Path, Type Request)[] expected =
        [
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/draft-operations/{operation_id}", typeof(DraftCriteriaEdition)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/initial-label-operations/{operation_id}", typeof(ChooseInitialEditionLabel)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/draft-cancellations/{operation_id}", typeof(CancelCriteriaEditionDraftRequest)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/rename-operations/{operation_id}", typeof(BeginCriteriaEditionRename)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/rename-cancellations/{operation_id}", typeof(CancelCriteriaEditionRename)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/abandon-operations/{operation_id}", typeof(AbandonCriteriaEditionDraft)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/publication-attempts/{attempt_id}", typeof(BeginCriteriaPublication)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/entries/{identifier}/change-operations/{operation_id}", typeof(ProposeCriterionChange)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/entries/{identifier}/summary-operations/{operation_id}", typeof(CorrectCriterionSummary)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/fields/{field}/operations/{operation_id}", typeof(SetCriteriaEditionField)),
            ("PUT", "/api/v1/platform/criteria-editions/{edition_id}/disclosures/{category}/{code}/operations/{operation_id}", typeof(SetCriteriaEditionDisclosure)),
            ("GET", "/api/v1/platform/criteria-editions/{edition_id}/draft", typeof(GetCriteriaEditionDraft)),
            ("GET", "/api/v1/platform/criteria-editions/{edition_id}/operations/{operation_id}", typeof(GetCriteriaOperation)),
            ("GET", "/api/v1/platform/criteria-editions/{edition_id}/value-operations/{operation_id}", typeof(GetCriteriaEditionValueOperation)),
        ];

        // Act
        app.MapCriteriaAuthoringEndpoints();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().ToArray();

        // Assert
        Assert.Equal(expected.Length, routes.Length);
        foreach (var (method, path, request) in expected)
        {
            var route = Assert.Single(routes, candidate => candidate.RoutePattern.RawText == path &&
                candidate.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method) == true);
            var operation = Assert.IsType<PortiaOpenApiOperation>(route.Metadata.GetMetadata<PortiaOpenApiOperation>());
            Assert.Equal(request, operation.RequestType);
            Assert.Contains(route.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                authorization => authorization.Policy == "BdgrzApiUser");
        }
    }

    [Fact]
    public async Task ShouldBindExactClientIdentityAndValueTargetGivenAuthoringRoutes()
    {
        // Arrange
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapCriteriaAuthoringEndpoints();
        var operations = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetMetadata<PortiaOpenApiOperation>()).OfType<PortiaOpenApiOperation>().ToArray();

        // Act
        var publication = Assert.Single(operations, operation => operation.RequestType == typeof(BeginCriteriaPublication));
        var entry = Assert.Single(operations, operation => operation.RequestType == typeof(ProposeCriterionChange));
        var valueRead = Assert.Single(operations, operation => operation.RequestType == typeof(GetCriteriaEditionValueOperation));
        var receipt = Assert.Single(operations, operation => operation.RequestType == typeof(GetCriteriaOperation));

        // Assert
        AssertBinding(publication, "AttemptId", "route", "attempt_id");
        AssertBinding(entry, "EditionId", "route", "edition_id");
        AssertBinding(entry, "Identifier", "route", "identifier");
        AssertBinding(entry, "OperationId", "route", "operation_id");
        AssertBinding(valueRead, "OperationId", "route", "operation_id");
        AssertBinding(valueRead, "TargetKind", "query", "target_kind");
        AssertBinding(valueRead, "Field", "query", "field");
        AssertBinding(valueRead, "Category", "query", "category");
        AssertBinding(valueRead, "Code", "query", "code");
        AssertBinding(receipt, "Identifier", "query", "identifier");
        Assert.Equal(typeof(CriteriaOperationReceipt), receipt.ResultType);
    }

    static void AssertBinding(PortiaOpenApiOperation operation, string name, string source, string wireName)
    {
        var parameter = Assert.Single(operation.Parameters, candidate => candidate.ClrName == name);
        Assert.Equal(source, parameter.Source);
        Assert.Equal(wireName, parameter.WireName ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(parameter.ClrName));
    }
}
