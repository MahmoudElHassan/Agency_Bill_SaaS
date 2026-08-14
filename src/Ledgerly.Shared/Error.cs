namespace Ledgerly.Shared;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("none", "No error");
    public static readonly Error PlanLimit = new("plan_limit", "Plan limit reached");
    public static readonly Error Validation = new("validation", "Validation failed");
    public static readonly Error NotFound = new("not_found", "Resource not found");
    public static readonly Error Unauthorized = new("unauthorized", "Unauthorized");
    public static readonly Error Forbidden = new("forbidden", "Forbidden");
    public static readonly Error Conflict = new("conflict", "Conflict");
    public static readonly Error InvalidState = new("invalid_state", "Invalid state transition");

    public static Error FromMessage(string code, string message) => new(code, message);
}