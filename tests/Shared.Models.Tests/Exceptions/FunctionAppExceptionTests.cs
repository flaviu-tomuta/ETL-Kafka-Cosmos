using Shared.Models.Exceptions;

namespace Shared.Models.Tests.Exceptions;

public sealed class FunctionAppExceptionTests
{
    // ── MessageValidationException ──────────────────────────────────────────

    [Fact]
    public void MessageValidationException_CaughtAsFunctionAppException_ExposesEntityIdAndMessageId()
    {
        FunctionAppException caught;

        try
        {
            throw new MessageValidationException("bad header", "entity-1", "msg-1");
        }
        catch (FunctionAppException ex)
        {
            caught = ex;
        }

        Assert.Equal("entity-1", caught.EntityId);
        Assert.Equal("msg-1", caught.MessageId);
    }

    [Fact]
    public void MessageValidationException_IsSealed()
    {
        Assert.True(typeof(MessageValidationException).IsSealed);
    }

    [Fact]
    public void MessageValidationException_WithoutInnerException_InnerExceptionIsNull()
    {
        MessageValidationException ex = new("bad payload", "entity-2", "msg-2");

        Assert.Null(ex.InnerException);
        Assert.Equal("bad payload", ex.Message);
    }

    [Fact]
    public void MessageValidationException_WithInnerException_RetainsOriginalException()
    {
        InvalidOperationException inner = new("parse failed");
        MessageValidationException ex = new("bad payload", "entity-3", "msg-3", inner);

        Assert.Same(inner, ex.InnerException);
    }

    // ── BusinessRuleViolationException ──────────────────────────────────────

    [Fact]
    public void BusinessRuleViolationException_ExposesRuleNameAlongsideEntityIdAndMessageId()
    {
        BusinessRuleViolationException ex = new("rule violated", "entity-4", "msg-4", "DuplicateRecord");

        Assert.Equal("DuplicateRecord", ex.RuleName);
        Assert.Equal("entity-4", ex.EntityId);
        Assert.Equal("msg-4", ex.MessageId);
    }

    [Fact]
    public void BusinessRuleViolationException_IsSealed()
    {
        Assert.True(typeof(BusinessRuleViolationException).IsSealed);
    }

    [Fact]
    public void BusinessRuleViolationException_WithInnerException_RetainsOriginalException()
    {
        ArgumentException inner = new("invalid op");
        BusinessRuleViolationException ex = new("rule violated", "entity-5", "msg-5", "RecordNotFound", inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void BusinessRuleViolationException_IsFunctionAppException()
    {
        BusinessRuleViolationException ex = new("rule violated", "entity-6", "msg-6", "IsPreferredConflict");

        Assert.IsAssignableFrom<FunctionAppException>(ex);
    }

    // ── EnrichmentStepException ─────────────────────────────────────────────

    [Fact]
    public void EnrichmentStepException_ExposesStepNameAlongsideEntityIdAndMessageId()
    {
        EnrichmentStepException ex = new("step failed", "entity-7", "msg-7", "AddressEnrichmentStep");

        Assert.Equal("AddressEnrichmentStep", ex.StepName);
        Assert.Equal("entity-7", ex.EntityId);
        Assert.Equal("msg-7", ex.MessageId);
    }

    [Fact]
    public void EnrichmentStepException_IsSealed()
    {
        Assert.True(typeof(EnrichmentStepException).IsSealed);
    }

    [Fact]
    public void EnrichmentStepException_WithInnerException_RetainsOriginalException()
    {
        HttpRequestException inner = new("timeout");
        EnrichmentStepException ex = new("step failed", "entity-8", "msg-8", "CreditCheckEnrichmentStep", inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void EnrichmentStepException_IsFunctionAppException()
    {
        EnrichmentStepException ex = new("step failed", "entity-9", "msg-9", "ComplianceEnrichmentStep");

        Assert.IsAssignableFrom<FunctionAppException>(ex);
    }

    // ── FunctionAppException (abstract base) ────────────────────────────────

    [Fact]
    public void FunctionAppException_IsAbstract()
    {
        Assert.True(typeof(FunctionAppException).IsAbstract);
    }

    [Fact]
    public void FunctionAppException_InheritFromException()
    {
        Assert.True(typeof(FunctionAppException).IsSubclassOf(typeof(Exception)));
    }
}
