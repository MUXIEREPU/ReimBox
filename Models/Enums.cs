namespace ReimbursementAssistant.Models;
public enum InvoiceCategory { Unknown, Consumable, Travel, PrintFee, Other }
public enum TravelSubCategory { None, Flight, Train, Hotel, Taxi, RentalCar, Toll, Fuel, OtherTravel }
public enum AttachmentType { Invoice, PaymentProof, OrderPage, ThreeDPrintDetails, Other }
public enum RecordStatus { Analyzing, Complete, MissingDocuments, NeedConfirmation, Error, Ignored }
public enum RecognitionSource { None, NativePdfText, PaddleOcrVl16, WindowsOcr }
public enum ValidationSeverity { Warning, Error }
public enum InvoiceListFilter { All, Consumable, Travel, PrintFee, Other, Pending, Duplicate }
