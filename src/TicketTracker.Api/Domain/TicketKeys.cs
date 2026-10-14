namespace TicketTracker.Api.Domain;

public static class TicketKeys
{
    public static string Format(string projectKey, int number)
    {
        if (!ProjectKeys.IsValid(projectKey))
        {
            throw new ArgumentException("Project key must be 2 to 10 uppercase letters.", nameof(projectKey));
        }

        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Ticket number must be at least 1.");
        }

        return $"{projectKey}-{number}";
    }
}
