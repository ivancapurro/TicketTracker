namespace TicketTracker.Api.Errors;

public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(StatusCodes.Status404NotFound, "Not Found", message)
    {
    }
}

public sealed class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "Conflict", message)
    {
    }
}

public sealed class BadRequestException : ApiException
{
    public BadRequestException(string message)
        : base(StatusCodes.Status400BadRequest, "Bad Request", message)
    {
    }
}
