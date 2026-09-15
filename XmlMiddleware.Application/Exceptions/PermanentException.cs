namespace XmlMiddleware.Application.Exceptions;

public class PermanentException : Exception
{
    public PermanentException(string message)
        : base(message)
    {
    }
}