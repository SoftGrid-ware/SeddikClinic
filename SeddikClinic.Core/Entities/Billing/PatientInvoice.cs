namespace SeddikClinic.Core.Entities.Billing;

public class PatientInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; } // المبلغ الإجمالي المطلوب
    public decimal PaidAmount { get; set; }  // المبلغ المسدد فعلياً
    public decimal RemainingAmount => Math.Max(0, TotalAmount - PaidAmount); // المبلغ المستحق غير المحصل

    public bool IsFullyPaid => RemainingAmount <= 0;
    public string? ServiceName { get; set; } // اسم الخدمة الرئيسية (مثلاً: زراعة أسنان، تقويم، حشو تجميلي)
    public decimal DirectCostEstimate { get; set; } // تكلفة المواد والمعمل المباشرة لتقدير الربح التشغيلي

    public ICollection<PatientPayment> Payments { get; set; } = new List<PatientPayment>();
}
