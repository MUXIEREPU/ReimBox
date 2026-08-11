namespace ReimbursementAssistant.Models;

public enum NamingField
{
    Number,
    Category,
    ItemDescription,
    TotalAmount,
    InvoiceDate,
    SellerName,
    InvoiceNumber
}

public enum NamingApplyTarget
{
    ExportOnly,
    RenameOriginalFiles
}

public sealed class InvoiceNamingRule
{
    public List<NamingField> Fields { get; } = [];
    public string Separator { get; set; } = "_";

    public static InvoiceNamingRule Default()
    {
        var rule = new InvoiceNamingRule();
        rule.Fields.Add(NamingField.Number);
        rule.Fields.Add(NamingField.ItemDescription);
        rule.Fields.Add(NamingField.TotalAmount);
        return rule;
    }

    public InvoiceNamingRule Clone()
    {
        var clone = new InvoiceNamingRule { Separator = Separator };
        clone.Fields.AddRange(Fields);
        return clone;
    }
}
