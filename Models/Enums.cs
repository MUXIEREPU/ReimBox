namespace ReimbursementAssistant.Models;
public enum InvoiceCategory
{
    Unknown = 0,
    Consumable = 1,
    Travel = 2,
    PrintFee = 3,
    Other = 4,
    ShippingFee = 5
}
public enum TravelSubCategory
{
    None = 0,
    Flight = 1,
    Train = 2,
    Hotel = 3,
    Taxi = 4,
    RentalCar = 5,
    Toll = 6,
    Fuel = 7,
    OtherTravel = 8,
    Meal = 9
}
public enum AttachmentType { Invoice, PaymentProof, OrderPage, ThreeDPrintDetails, Other }
public enum RecordStatus { Analyzing, Complete, MissingDocuments, NeedConfirmation, Error, Ignored }
public enum RecognitionSource { None, NativePdfText, PaddleOcrVl16, WindowsOcr }
public enum ValidationSeverity { Warning, Error }
public enum InvoiceListFilter { All, Consumable, Travel, PrintFee, ShippingFee, Other, Pending, Duplicate }
