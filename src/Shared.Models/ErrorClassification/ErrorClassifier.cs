using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Shared.Models.ErrorClassification;

public sealed class ErrorClassifier : IErrorClassifier
{
    public ErrorCategory Classify(Exception ex) => ex switch
    {
        HttpRequestException http when IsTransientHttpStatus(http) => ErrorCategory.Transient,
        CosmosException cosmos when cosmos.StatusCode == HttpStatusCode.TooManyRequests => ErrorCategory.Transient,
        CosmosException cosmos when cosmos.StatusCode == HttpStatusCode.PreconditionFailed => ErrorCategory.Transient,
        TaskCanceledException => ErrorCategory.Transient,
        TimeoutException => ErrorCategory.Transient,
        OperationCanceledException => ErrorCategory.Transient,
        JsonException => ErrorCategory.Permanent,
        MessageValidationException => ErrorCategory.Permanent,
        BusinessRuleViolationException => ErrorCategory.Permanent,
        CosmosException cosmos when cosmos.StatusCode == HttpStatusCode.BadRequest => ErrorCategory.Permanent,
        _ => ErrorCategory.Unknown
    };

    private static bool IsTransientHttpStatus(HttpRequestException ex) =>
        ex.StatusCode is HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.TooManyRequests;
}
