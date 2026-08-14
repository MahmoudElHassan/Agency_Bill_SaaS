namespace Ledgerly.Domain.Enums;

public enum Plan
{
    Free = 0,
    Pro = 1,
    Business = 2
}

public enum PlanStatus
{
    Inactive = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3
}

public enum InvoiceStatus
{
    Draft = 0,
    Sent = 1,
    Paid = 2,
    Overdue = 3,
    Void = 4
}

public enum TenantRole
{
    Owner = 0,
    Staff = 1
}