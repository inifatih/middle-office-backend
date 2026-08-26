namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    // Full field breakdown of PRD §7.3.1 (Kajian Harian Risiko Likuiditas), sections A-H.
    // Derived/traffic-light fields are computed by KajianCalculationService, never uploaded as-is.
    public class LiquidityDailyReview
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public UploadBatch Batch { get; set; } = null!;
        public DateOnly Tanggal { get; set; }

        // A. Primary Reserve Rupiah
        public string KondisiKasRupiah { get; set; } = string.Empty; // Normal / Tight
        public decimal PaguKasRupiahBniWide { get; set; }
        public decimal KasRupiah { get; set; }
        public decimal PersenTerhadapPaguRupiah { get; set; } // computed
        public decimal RataRataRealisasiKasRupiah { get; set; }
        public string TrafficLightKasRupiah { get; set; } = string.Empty; // computed

        public decimal GwmHarianRupiah { get; set; } // min 0%
        public string GwmHarianRupiahStatus { get; set; } = string.Empty; // computed OVER/UNDER
        public decimal RcBiRupiah { get; set; }
        public decimal DpkRupiah { get; set; }
        public decimal GwmAveragingRupiah { get; set; }
        public string GwmAveragingRupiahStatus { get; set; } = string.Empty; // computed
        public decimal PemenuhanGwmRupiah { get; set; }
        public decimal ExcessReserveRupiah { get; set; }
        public decimal GwmSekunderPlmRupiah { get; set; }
        public string GwmSekunderPlmRupiahStatus { get; set; } = string.Empty; // computed
        public decimal SbiSdbiSbnRupiah { get; set; }
        public decimal KetentuanGwmRupiah { get; set; } // regulatory reference, from ConfigParameter
        public decimal KetentuanGwmSekunderPlmRupiah { get; set; }

        // B. Primary Reserve Valas
        public decimal KasValas { get; set; }
        public decimal PaguKasValasBniWide { get; set; }
        public decimal PersenTerhadapPaguValas { get; set; } // computed
        public decimal RataRataRealisasiKasValas { get; set; }
        public string TrafficLightKasValas { get; set; } = string.Empty; // computed

        public decimal GwmValas { get; set; }
        public string GwmValasStatus { get; set; } = string.Empty; // computed
        public decimal RcBiValas { get; set; }
        public decimal DpkValas { get; set; }
        public decimal KetentuanGwmValas { get; set; }

        // C. Safety Level Rupiah
        public string TightNormalRupiah { get; set; } = string.Empty;
        public decimal SafetyLevelRupiah { get; set; }
        public decimal CadanganLikuiditasRupiah { get; set; }
        public string TrafficLightCadanganRupiah { get; set; } = string.Empty; // computed
        public decimal TrLiquidRupiah { get; set; } // after haircut 20%
        public string StatusLikuiditasRupiah { get; set; } = string.Empty; // computed

        // D. Safety Level Valas
        public string TightNormalValas { get; set; } = string.Empty;
        public decimal SafetyLevelValas { get; set; }
        public decimal CadanganLikuiditasValas { get; set; }
        public string TrafficLightCadanganValas { get; set; } = string.Empty; // computed
        public decimal TrLiquidValas { get; set; }
        public string StatusLikuiditasValas { get; set; } = string.Empty; // computed

        // E. Rasio Intermediasi Makroprudensial (RIM)
        public decimal RimKredit { get; set; }
        public decimal RimSuratBerhargaDimiliki { get; set; }
        public decimal RimWeselEkspor { get; set; }
        public decimal RimDpk { get; set; }
        public decimal RimPinjamanYangDiterima { get; set; }
        public decimal RimSuratBerhargaDiterbitkan { get; set; }
        public decimal RimPercent { get; set; } // computed
        public string RimPosisi { get; set; } = string.Empty; // computed
        public decimal RimDisinsentif { get; set; } // computed

        // F. Insentif KLM
        public decimal InsentifKlm { get; set; }

        // G. Loan to Deposit Ratio
        public decimal LdrRupiah { get; set; }
        public decimal LdrValas { get; set; }
        public decimal LdrTotal { get; set; } // computed

        // H. AL:DPK dan AL:NCD
        public decimal AlNcd { get; set; } // min 50%
        public decimal AlDpk { get; set; } // min 10%
    }
}
