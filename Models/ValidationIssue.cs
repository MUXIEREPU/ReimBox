namespace ReimbursementAssistant.Models;
public sealed record ValidationIssue(string Code, ValidationSeverity Severity, string Message);
