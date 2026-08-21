namespace BestStories.Application.Exceptions;

public class ExternalApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class ExternalApiTimeoutException(string message, Exception? innerException = null)
    : ExternalApiException(message, innerException);

public class ExternalApiRateLimitedException(string message, Exception? innerException = null)
    : ExternalApiException(message, innerException);

public class ExternalApiUnavailableException(string message, Exception? innerException = null)
    : ExternalApiException(message, innerException);
