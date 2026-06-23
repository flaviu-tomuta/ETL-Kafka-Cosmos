using Shared.Models.Models;

namespace Shared.Models.ErrorClassification;

public interface IErrorClassifier
{
    ErrorCategory Classify(Exception ex);
}
