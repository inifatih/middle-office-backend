namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    // One row per upload/submission across all 4 report types (PRD PRD §10 mio.UploadBatch).
    // Detail rows (LiquidityDailyReview / MaturityProfileHo / MaturityProfileKln) hang off this via BatchId.
    public class UploadBatch
    {
        public int Id { get; set; }
        public ReportType ReportType { get; set; }
        public MaturityTipe? MaturityTipe { get; set; } // only used when ReportType == ResumeHo
        public DateOnly Period { get; set; } // tanggal (KajianRisiko) or first-of-month (ResumeHo / ProfilKln)
        public BatchStatus Status { get; set; } = BatchStatus.Draft;

        public string FileName { get; set; } = string.Empty;

        public int UploaderId { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public int? ApproverId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectReason { get; set; }
    }
}
