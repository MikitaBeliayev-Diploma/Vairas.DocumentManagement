using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace Vairas.DocumentManagement.Configuration;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info.Title = "Vairas Document Management"; document.Info.Version = "1.0.0";
        document.Info.Description = "Versioned templates, immutable PDF/DOCX artifacts, human-reviewed publication and authenticated file delivery. Completion reports are internal handoff artifacts.";
        document.Components ??= new(); document.Components.SecuritySchemes = new Dictionary<string,IOpenApiSecurityScheme> { ["bearerAuth"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" } };
        foreach (var path in document.Paths.Values)
            foreach (var pair in path.Operations ?? [])
            {
                var operation = pair.Value;
                operation.Security = [new() { [new OpenApiSecuritySchemeReference("bearerAuth",document)] = [] }];
                operation.Responses ??= new();
                foreach (var code in new[] { "400","401","403","404","409","422","429","503" }) operation.Responses.TryAdd(code,new OpenApiResponse { Description = code switch { "400" => "Invalid input", "401" => "Authentication required", "403" => "Access denied", "404" => "Resource missing or concealed", "409" => "Revision or idempotency conflict", "422" => "Business rule rejected", "429" => "Rate limit exceeded", _ => "Dependency unavailable; retry the same operation" }, Content = new Dictionary<string,OpenApiMediaType> { ["application/problem+json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object, Required = new HashSet<string> { "title","status","code","correlationId" }, Properties = new Dictionary<string,IOpenApiSchema> { ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer },["code"] = new OpenApiSchema { Type = JsonSchemaType.String },["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String } } } } } });
                if (pair.Key != HttpMethod.Get && pair.Key != HttpMethod.Head)
                { operation.Parameters ??= []; operation.Parameters.Add(new OpenApiParameter { Name = "Idempotency-Key", In = ParameterLocation.Header, Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" } }); }
                foreach (var p in operation.Parameters ?? []) if (p is OpenApiParameter parameter && p.Name == "schoolId") parameter.Required = true;
            }
        return Task.CompletedTask;
    }
}
