using System.Net;
using System.Text;
using System.Text.Json;
using Amendment.Function.Pipeline;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Kafka;
using Microsoft.Extensions.Logging;
using Shared.Models.Contracts;
using Shared.Models.ErrorClassification;
using Shared.Models.Kafka;
using Shared.Models.Models;

namespace Amendment.Function.Functions;

public sealed class AmendmentKafkaFunction
{
    private readonly IAmendmentPipeline _pipeline;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IErrorClassifier _errorClassifier;
    private readonly IDeadLetterService _deadLetterService;
    private readonly IRetryService _retryService;
    private readonly ILogger<AmendmentKafkaFunction> _logger;

    public AmendmentKafkaFunction(
        IAmendmentPipeline pipeline,
        IIdempotencyService idempotencyService,
        IErrorClassifier errorClassifier,
        IDeadLetterService deadLetterService,
        IRetryService retryService,
        ILogger<AmendmentKafkaFunction> logger)
    {
        _pipeline = pipeline;
        _idempotencyService = idempotencyService;
        _errorClassifier = errorClassifier;
        _deadLetterService = deadLetterService;
        _retryService = retryService;
        _logger = logger;
    }

    [Function("AmendmentKafkaFunction")]
    public async Task Run(
        [KafkaTrigger(
            "%KafkaTopic%",
            "%KafkaBootstrapServers%",
            ConsumerGroup = "amendment-func",
            Protocol = BrokerProtocol.Plaintext)]
        KafkaRecord[] messages)
    {
        IEnumerable<KafkaRawEvent> events = messages.Select(m =>
            new KafkaRawEvent(
                RawPayload: Encoding.UTF8.GetString(m.Value ?? []),
                Headers: ExtractHeaders(m),
                Topic: m.Topic ?? string.Empty,
                Partition: m.Partition,
                Offset: m.Offset
            ));

        await ProcessBatchAsync(events);
    }

    internal async Task ProcessBatchAsync(IEnumerable<KafkaRawEvent> events)
    {
        int total = 0, succeeded = 0, transientFailures = 0, permanentFailures = 0;

        foreach (KafkaRawEvent rawEvent in events)
        {
            total++;
            KafkaMessageContext? ctx = null;
            try
            {
                string partyId = ExtractPartyId(rawEvent.RawPayload);
                ctx = KafkaMessageContextBuilder.Build(
                    rawEvent.Headers,
                    partyId,
                    rawEvent.Topic,
                    rawEvent.Partition,
                    rawEvent.Offset,
                    topicRole: "amendment");

                using (_logger.BeginScope(KafkaMessageContextBuilder.BuildLogScope(ctx)))
                {
                    if (await _idempotencyService.IsDuplicateAsync(ctx.MessageId))
                    {
                        _logger.LogInformation(
                            "DuplicateMessageSkipped {MessageId} {EntityId} {TopicRole}",
                            ctx.MessageId, ctx.PartyId, ctx.TopicRole);
                        succeeded++;
                        continue;
                    }

                    await _pipeline.ProcessAsync(ctx, rawEvent.RawPayload);
                    await _idempotencyService.MarkProcessedAsync(ctx.MessageId);
                    succeeded++;
                }
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorCategory category = _errorClassifier.Classify(ex);
                string msgId = ctx?.MessageId ?? "unknown";
                string entityId = ctx?.PartyId ?? "unknown";

                _logger.LogError(ex,
                    "MessageProcessingFailed {MessageId} {EntityId} {Category} {ExceptionType}",
                    msgId, entityId, category, ex.GetType().Name);

                if (category == ErrorCategory.Permanent)
                {
                    permanentFailures++;
                    if (ctx is not null)
                        await _deadLetterService.SendAsync(ctx, rawEvent.RawPayload, ex.Message, attempt: 1);
                }
                else
                {
                    transientFailures++;
                    if (ctx is not null)
                    {
                        bool isImmediate = ex is CosmosException ce
                            && ce.StatusCode == HttpStatusCode.PreconditionFailed;
                        await _retryService.EnqueueAsync(ctx, rawEvent.RawPayload, attemptCount: 1, isImmediate);
                    }
                }
            }
        }

        _logger.LogWarning(
            "BatchCompleted total={Total} succeeded={Succeeded} transientFailures={Transient} permanentFailures={Permanent}",
            total, succeeded, transientFailures, permanentFailures);
    }

    private static string ExtractPartyId(string rawPayload)
    {
        using JsonDocument doc = JsonDocument.Parse(rawPayload);
        if (doc.RootElement.TryGetProperty("partyId", out JsonElement element))
            return element.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> ExtractHeaders(KafkaRecord message)
    {
        Dictionary<string, string> result = new();
        if (message.Headers is null) return result;

        foreach (KafkaHeader header in message.Headers)
            result[header.Key] = Encoding.UTF8.GetString(header.Value ?? []);

        return result;
    }
}
