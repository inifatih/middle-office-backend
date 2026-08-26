namespace middle_office_backend.Rmis.Application.Modules.MiddleOffice.Dtos
{
    public class ExtractResponseDto<T>
    {
        public int TotalRows { get; set; }
        public string? Message { get; set; }
        public string? Periode { get; set; }
        public List<T> Data { get; set; } = new List<T>();
    }

    // =====================================================================
    // 1. Kajian Risiko Likuiditas — sheet "upload"
    // =====================================================================

    // One row exactly as it appears in the sheet, hierarchy preserved (audit/raw view).
    public class KajianRisikoRowDto
    {
        public string? SectionCode { get; set; }   // col A: "A.", "B." ... (section header rows only)
        public string? SubNumber { get; set; }     // col B: "1.", "2.", "a.", "b." ...
        public string? Keterangan { get; set; }    // col C: description text
        public string? Nilai { get; set; }         // col E: raw value as displayed (percentages, numbers, text)
        public string? Status { get; set; }        // col F: e.g. "103.98%", "DARK GREEN", "6.36% OVER"
        public int IndentLevel { get; set; }       // 0=section, 1=numbered item, 2=lettered sub-item, 3=bullet (▫) leaf
    }

    // Named, typed fields per PRD §7.3.1 (sections A-H) — this is what the dashboard/calc engine consumes.
    // Every field is nullable: a row not found in the sheet (template drift) simply comes back null
    // instead of silently defaulting to 0, which is what made the old extractor's numbers untrustworthy.
    public class KajianRisikoSummaryDto
    {
        public string? Periode { get; set; }

        // A. Primary Reserve Rupiah
        public string? KondisiKasRupiah { get; set; }
        public decimal? PaguKasRupiahBniWide { get; set; }
        public decimal? KasRupiah { get; set; }
        public decimal? PersenTerhadapPaguRupiah { get; set; }
        public decimal? RataRataRealisasiKasRupiah { get; set; }
        public string? TrafficLightKasRupiah { get; set; }

        public decimal? GwmHarianRupiah { get; set; }
        public string? GwmHarianRupiahStatus { get; set; }
        public decimal? RcBiRupiah { get; set; }
        public decimal? DpkRupiah { get; set; }
        public decimal? GwmAveragingRupiah { get; set; }
        public string? GwmAveragingRupiahStatus { get; set; }
        public decimal? PemenuhanGwmRupiah { get; set; }
        public decimal? ExcessReserveRupiah { get; set; }
        public decimal? GwmSekunderPlmRupiah { get; set; }
        public string? GwmSekunderPlmRupiahStatus { get; set; }
        public decimal? SbiSdbiSbnRupiah { get; set; }
        public decimal? KetentuanGwmRupiah { get; set; }
        public decimal? KetentuanGwmSekunderPlmRupiah { get; set; }

        // B. Primary Reserve Valas
        public decimal? KasValas { get; set; }
        public decimal? PaguKasValasBniWide { get; set; }
        public decimal? PersenTerhadapPaguValas { get; set; }
        public decimal? RataRataRealisasiKasValas { get; set; }
        public string? TrafficLightKasValas { get; set; }
        public decimal? GwmValas { get; set; }
        public string? GwmValasStatus { get; set; }
        public decimal? RcBiValas { get; set; }
        public decimal? DpkValas { get; set; }
        public decimal? KetentuanGwmValas { get; set; }

        // C. Safety Level Rupiah
        public string? TightNormalRupiah { get; set; }
        public decimal? SafetyLevelRupiah { get; set; }
        public decimal? CadanganLikuiditasRupiah { get; set; }
        public string? TrafficLightCadanganRupiah { get; set; }
        public decimal? TrLiquidRupiah { get; set; }
        public string? StatusLikuiditasRupiah { get; set; }

        // D. Safety Level Valas
        public string? TightNormalValas { get; set; }
        public decimal? SafetyLevelValas { get; set; }
        public decimal? CadanganLikuiditasValas { get; set; }
        public string? TrafficLightCadanganValas { get; set; }
        public decimal? TrLiquidValas { get; set; }
        public string? StatusLikuiditasValas { get; set; }

        // E. Rasio Intermediasi Makroprudensial (RIM)
        public decimal? RimKredit { get; set; }
        public decimal? RimSuratBerhargaDimiliki { get; set; }
        public decimal? RimWeselEkspor { get; set; }
        public decimal? RimDpk { get; set; }
        public decimal? RimPinjamanYangDiterima { get; set; }
        public decimal? RimSuratBerhargaDiterbitkan { get; set; }
        public decimal? RimPercent { get; set; }
        public string? RimPosisi { get; set; }
        public decimal? RimDisinsentif { get; set; }

        // F. Insentif KLM
        public decimal? InsentifKlm { get; set; }

        // G. Loan to Deposit Ratio
        public decimal? LdrRupiah { get; set; }
        public decimal? LdrValas { get; set; }
        public decimal? LdrTotal { get; set; }

        // H. AL:DPK dan AL:NCD
        public decimal? AlNcd { get; set; }
        public decimal? AlDpk { get; set; }
    }

    public class KajianRisikoExtractResponseDto
    {
        public int TotalRows { get; set; }
        public string? Message { get; set; }
        public List<KajianRisikoRowDto> Rows { get; set; } = new();
        public KajianRisikoSummaryDto Summary { get; set; } = new();
        public List<string> UnmatchedFields { get; set; } = new(); // known fields the sheet had no row for
    }

    // =====================================================================
    // 2. Profil Maturitas & Reserve Requirement KLN — sheet "upload"
    // =====================================================================

    public class ProfilMaturitasKlnDto
    {
        public string? Cabang { get; set; }
        public double Aset { get; set; }
        public double Kewajiban { get; set; }
        public double Selisih { get; set; }
        public double ProfilMaturitasPercent { get; set; }
        public string? ReserveRequirement { get; set; }
        public string? TrafficLight { get; set; }
    }

    // =====================================================================
    // 3. Resume Maturity Profile HO (Contractual / Behavioral) — sheet "Kontraktual" / "Behavioral"
    // =====================================================================

    public class ResumeMatProfHoDto
    {
        public string? KategoriNeraca { get; set; }
        public string? Sandi { get; set; }
        public double? NilaiIdr { get; set; }
        public double? NilaiVa { get; set; }
    }

    // Named summary mirroring PRD §7.3.2/§7.3.3 — sandi 10000..80000 plus the Behavioral-only
    // Gap Maturitas / Traffic Light rows, which the old extractor dropped entirely because they
    // have no "Sandi" value and were filtered out.
    public class ResumeMatProfHoSummaryDto
    {
        public string Tipe { get; set; } = string.Empty; // "Kontraktual" | "Behavioral"
        public string? Periode { get; set; }

        public decimal? AssetIdr { get; set; }            // 10000
        public decimal? AssetVa { get; set; }
        public decimal? KewajibanIdr { get; set; }         // 20000
        public decimal? KewajibanVa { get; set; }
        public decimal? SelisihNeracaIdr { get; set; }     // 30000
        public decimal? SelisihNeracaVa { get; set; }
        public decimal? KumulatifVa { get; set; }          // "D. Kumulatif" (VA only in source)

        public decimal? TagihanRekAdmIdr { get; set; }     // 40000
        public decimal? TagihanRekAdmVa { get; set; }
        public decimal? KewajibanRekAdmIdr { get; set; }   // 50000
        public decimal? KewajibanRekAdmVa { get; set; }
        public decimal? SelisihRekAdmIdr { get; set; }     // 60000
        public decimal? SelisihRekAdmVa { get; set; }

        public decimal? SelisihGabunganIdr { get; set; }   // 70000
        public decimal? SelisihGabunganVa { get; set; }
        public decimal? SelisihKumulatifIdr { get; set; }  // 80000
        public decimal? SelisihKumulatifVa { get; set; }

        public decimal? GapMaturitasIdr { get; set; }      // Behavioral only
        public decimal? GapMaturitasVa { get; set; }
        public string? TrafficLightIdr { get; set; }        // Behavioral only
        public string? TrafficLightVa { get; set; }
    }

    public class ResumeMatProfHoExtractResponseDto
    {
        public int TotalRows { get; set; }
        public string? Message { get; set; }
        public List<ResumeMatProfHoDto> Rows { get; set; } = new();
        public ResumeMatProfHoSummaryDto Summary { get; set; } = new();
    }
}
