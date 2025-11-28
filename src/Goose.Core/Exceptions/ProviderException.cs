namespace Goose.Core.Exceptions;

/// <summary>
/// Exception thrown when an AI provider encounters an error
/// </summary>
public class ProviderException : Exception
{
    /// <summary>
    /// The HTTP status code if applicable
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// The provider name that threw the exception
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Creates a new ProviderException
    /// </summary>
    public ProviderException() : base()
    {
    }

    /// <summary>
    /// Creates a new ProviderException with a message
    /// </summary>
    /// <param name="message">Error message</param>
    public ProviderException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new ProviderException with a message and inner exception
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public ProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new ProviderException with detailed information
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="providerName">Name of the provider</param>
    /// <param name="statusCode">HTTP status code if applicable</param>
    public ProviderException(string message, string providerName, int? statusCode = null) : base(message)
    {
        ProviderName = providerName;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Creates a new ProviderException with detailed information and inner exception
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="providerName">Name of the provider</param>
    /// <param name="innerException">Inner exception</param>
    /// <param name="statusCode">HTTP status code if applicable</param>
    public ProviderException(string message, string providerName, Exception innerException, int? statusCode = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
        StatusCode = statusCode;
    }
}
