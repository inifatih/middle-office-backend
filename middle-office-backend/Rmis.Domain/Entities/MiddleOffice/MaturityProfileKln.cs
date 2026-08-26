namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    // PRD §7.3.4 — Maturity Profile & Reserve Requirement per Kantor Luar Negeri (KLN).
    public class MaturityProfileKln
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public UploadBatch Batch { get; set; } = null!;
        public DateOnly Periode { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public decimal Aset { get; set; }
        public decimal Kewajiban { get; set; }
        public decimal Selisih { get; set; } // computed = Aset - Kewajiban
        public decimal ProfilMaturitasPercent { get; set; } // computed
        public string ReserveRequirementStatus { get; set; } = string.Empty; // computed OK / NOT OK
        public string TrafficLight { get; set; } = string.Empty; // computed
    }

    public class Branch
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
