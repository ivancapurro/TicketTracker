namespace TicketTracker.Api.Domain;

public static class FieldLimits
{
    public const int ProjectKeyMin = 2;
    public const int ProjectKeyMax = 10;
    public const int ProjectNameMax = 100;
    public const int ProjectDescriptionMax = 2000;
    public const int TicketTitleMax = 200;
    public const int TicketDescriptionMax = 10_000;
    public const int UserNameMax = 200;
    public const int UserEmailMax = 320;
    public const int PageSizeMax = 100;
    public const int PageMax = 10_000;
}
