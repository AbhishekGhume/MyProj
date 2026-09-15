namespace XmlMiddleware.Application.Exceptions;

public class TransientException : Exception
{
    public TransientException(string message)
        : base(message)
    {
    }
}