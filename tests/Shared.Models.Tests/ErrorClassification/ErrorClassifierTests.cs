using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Shared.Models.ErrorClassification;
using Shared.Models.Exceptions;
using Shared.Models.Models;

namespace Shared.Models.Tests.ErrorClassification;

public sealed class ErrorClassifierTests
{
    private readonly IErrorClassifier _classifier = new ErrorClassifier();

    // --- CosmosException 429 → Transient ---

    [Fact]
    public void Classify_CosmosException429_ReturnsTransient()
    {
        CosmosException ex = new CosmosException(
            "Too Many Requests", HttpStatusCode.TooManyRequests, 0, "activity-1", 1.0);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- CosmosException 412 → Transient ---

    [Fact]
    public void Classify_CosmosException412_ReturnsTransient()
    {
        CosmosException ex = new CosmosException(
            "Precondition Failed", HttpStatusCode.PreconditionFailed, 0, "activity-2", 1.0);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- CosmosException 400 → Permanent ---

    [Fact]
    public void Classify_CosmosException400_ReturnsPermanent()
    {
        CosmosException ex = new CosmosException(
            "Bad Request", HttpStatusCode.BadRequest, 0, "activity-3", 1.0);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Permanent, result);
    }

    // --- JsonException → Permanent ---

    [Fact]
    public void Classify_JsonException_ReturnsPermanent()
    {
        JsonException ex = new JsonException("Invalid JSON");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Permanent, result);
    }

    // --- MessageValidationException → Permanent ---

    [Fact]
    public void Classify_MessageValidationException_ReturnsPermanent()
    {
        MessageValidationException ex = new MessageValidationException(
            "Missing header", "entity-1", "msg-1");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Permanent, result);
    }

    // --- BusinessRuleViolationException → Permanent ---

    [Fact]
    public void Classify_BusinessRuleViolationException_ReturnsPermanent()
    {
        BusinessRuleViolationException ex = new BusinessRuleViolationException(
            "Rule violation", "entity-2", "msg-2", "DuplicateRecord");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Permanent, result);
    }

    // --- TaskCanceledException → Transient ---

    [Fact]
    public void Classify_TaskCanceledException_ReturnsTransient()
    {
        TaskCanceledException ex = new TaskCanceledException("Timeout");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- TimeoutException → Transient ---

    [Fact]
    public void Classify_TimeoutException_ReturnsTransient()
    {
        TimeoutException ex = new TimeoutException("Timed out");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- OperationCanceledException → Transient ---

    [Fact]
    public void Classify_OperationCanceledException_ReturnsTransient()
    {
        OperationCanceledException ex = new OperationCanceledException("Cancelled");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- HttpRequestException ServiceUnavailable → Transient ---

    [Fact]
    public void Classify_HttpRequestExceptionServiceUnavailable_ReturnsTransient()
    {
        HttpRequestException ex = new HttpRequestException(
            "Service Unavailable", null, HttpStatusCode.ServiceUnavailable);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- HttpRequestException GatewayTimeout → Transient ---

    [Fact]
    public void Classify_HttpRequestExceptionGatewayTimeout_ReturnsTransient()
    {
        HttpRequestException ex = new HttpRequestException(
            "Gateway Timeout", null, HttpStatusCode.GatewayTimeout);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- HttpRequestException TooManyRequests → Transient ---

    [Fact]
    public void Classify_HttpRequestExceptionTooManyRequests_ReturnsTransient()
    {
        HttpRequestException ex = new HttpRequestException(
            "Too Many Requests", null, HttpStatusCode.TooManyRequests);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Transient, result);
    }

    // --- Unknown exception → Unknown ---

    [Fact]
    public void Classify_UnknownException_ReturnsUnknown()
    {
        InvalidOperationException ex = new InvalidOperationException("Unknown error");

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Unknown, result);
    }

    // --- HttpRequestException with non-transient status → Unknown ---

    [Fact]
    public void Classify_HttpRequestExceptionNonTransientStatus_ReturnsUnknown()
    {
        HttpRequestException ex = new HttpRequestException(
            "Not Found", null, HttpStatusCode.NotFound);

        ErrorCategory result = _classifier.Classify(ex);

        Assert.Equal(ErrorCategory.Unknown, result);
    }
}
