using System.Text;

namespace ProjectMonitoring.Domain;

public enum UserRole { Admin, Manager, Member }

public enum ProjectStatus { Planned, Active, OnHold, Completed, Cancelled }

public enum WorkTaskStatus { Todo, InProgress, Done }

public enum TaskPriority { Low, Medium, High }

public enum SystemLogLevel { Info, Warning, Error }

/// <summary>Converts enums to and from the snake_case codes used by the database and the API.</summary>
public static class EnumCodes
{
    public static string ToCode<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var code = new StringBuilder(name.Length + 2);
        foreach (var character in name)
        {
            if (char.IsUpper(character) && code.Length > 0)
            {
                code.Append('_');
            }

            code.Append(char.ToLowerInvariant(character));
        }

        return code.ToString();
    }

    public static bool IsValid<TEnum>(string? code) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Any(value => value.ToCode() == code);

    /// <summary>Returns <paramref name="fallback"/> when no code was supplied.</summary>
    public static TEnum ParseOr<TEnum>(string? code, TEnum fallback) where TEnum : struct, Enum =>
        code is null ? fallback : Parse<TEnum>(code);

    public static TEnum Parse<TEnum>(string code) where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (value.ToCode() == code)
            {
                return value;
            }
        }

        throw new DomainRuleException($"'{code}' is not a valid {typeof(TEnum).Name}.");
    }
}
