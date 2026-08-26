namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    // PRD §7.3.2 / §7.3.3 — Maturity Profile HO IDR & Valas (Contractual / Behavioral), s/d 1 Bulan.
    // One row per (batch, sandi code) — mirrors the mockup's repeating Sandi/IDR/VA table shape.
    public class MaturityProfileHo
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public UploadBatch Batch { get; set; } = null!;
        public DateOnly Periode { get; set; } // first day of the month

        public string Sandi { get; set; } = string.Empty; // 10000..80000
        public string Keterangan { get; set; } = string.Empty;
        public decimal NilaiIdr { get; set; }
        public decimal NilaiVa { get; set; }

        // Behavioral-only derived fields (PRD §7.3.3)
        public decimal? GapMaturitasIdr { get; set; } // computed
        public decimal? GapMaturitasVa { get; set; } // computed
        public string? TrafficLightIdr { get; set; } // computed
        public string? TrafficLightVa { get; set; } // computed
    }
}
