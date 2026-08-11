namespace ReimbursementAssistant.Models;

public sealed record ReimbursementInfo(string PersonName, string PersonIdentifier, string? Description)
{
    public bool HasRequiredFields => !string.IsNullOrWhiteSpace(PersonName) && !string.IsNullOrWhiteSpace(PersonIdentifier);
}
