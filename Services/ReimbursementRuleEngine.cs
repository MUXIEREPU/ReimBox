using ReimbursementAssistant.Configuration;
using ReimbursementAssistant.Models;
namespace ReimbursementAssistant.Services;
public sealed class ReimbursementRuleEngine(ReimbursementSettings settings)
{
    public void Evaluate(InvoiceRecord record)
    {
        record.RequiredDocuments.Clear(); record.RequiredDocuments.Add(AttachmentType.Invoice);
        if (record.Category == InvoiceCategory.Travel && record.SubCategory == TravelSubCategory.Flight && settings.RequireFlightOrderPage) record.RequiredDocuments.Add(AttachmentType.OrderPage);
        if (record.Category == InvoiceCategory.Travel && record.SubCategory == TravelSubCategory.Train && settings.RequireTrainOrderPage) record.RequiredDocuments.Add(AttachmentType.OrderPage);
        if (record.Category == InvoiceCategory.Travel && record.SubCategory == TravelSubCategory.Flight && settings.RequireFlightPaymentProof) record.RequiredDocuments.Add(AttachmentType.PaymentProof);
        if (record.Category == InvoiceCategory.Consumable && record.TotalAmount > settings.ConsumablePaymentThreshold) record.RequiredDocuments.Add(AttachmentType.PaymentProof);
        if (record.Category == InvoiceCategory.Consumable && record.IsThreeDPrinting && settings.RequireThreeDPrintDetails) record.RequiredDocuments.Add(AttachmentType.ThreeDPrintDetails);
        record.Status = record.UserIgnored ? RecordStatus.Ignored : record.ValidationIssues.Any(x => x.Severity == ValidationSeverity.Error) ? RecordStatus.Error : record.Category == InvoiceCategory.Unknown ? RecordStatus.NeedConfirmation : record.MissingTypes().Any() ? RecordStatus.MissingDocuments : RecordStatus.Complete;
        record.OnChanged(nameof(record.StatusDisplay)); record.OnChanged(nameof(record.RequirementDisplays));
    }
}
