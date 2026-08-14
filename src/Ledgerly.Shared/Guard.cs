namespace Ledgerly.Shared;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);
        return value;
    }

    public static decimal AgainstNegative(decimal value, string paramName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} cannot be negative.");
        return value;
    }

    public static int AgainstNonPositive(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be positive.");
        return value;
    }

    public static Guid AgainstEmptyGuid(Guid value, string paramName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{paramName} cannot be empty.", paramName);
        return value;
    }
}