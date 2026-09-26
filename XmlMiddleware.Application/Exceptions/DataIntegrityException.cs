namespace XmlMiddleware.Application.Exceptions;

public class DataIntegrityException : Exception
{
    public DataIntegrityException(string message)
        : base(message)
    {
    }
}