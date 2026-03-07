namespace Server.Data;

public static class Roles
{
    public const string President = "President";
    public const string Treasurer = "Treasurer";
    public const string Secretary = "Secretary";
    public const string Resident = "Resident";

    public static readonly string[] All = [President, Treasurer, Secretary, Resident];
    public static readonly string[] OfficerRoles = [President, Treasurer, Secretary];

    public const string OfficerPolicy = "Officer";
}
