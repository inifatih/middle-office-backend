using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Dtos;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace middle_office_backend.Rmis.Application.Modules.MiddleOffice.Services
{
    public class MiddleOfficeServices : IMiddleOfficeServices
    {
        private const string UploadSheetName = "upload";

        private bool IsExcelFile(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return extension == ".xlsx" || extension == ".xls";
        }

        // Reads a cell's typed value instead of its display string. ClosedXML's GetString()
        // formats numbers using the CURRENT THREAD CULTURE, and on this host's locale (en-ID,
        // which uses "," as the decimal separator) the old code path — GetString() followed by
        // ParseDecimal's `.Replace(",", "")` thousands-separator strip — silently destroyed every
        // fractional value (e.g. 1444597.58821 became 144459758821, a ~100,000x error). Reading
        // the typed value sidesteps culture entirely, so numbers round-trip correctly regardless
        // of server locale.
        private static string GetCellText(IXLCell cell)
        {
            if (cell.IsEmpty()) return string.Empty;

            // ClosedXML sometimes reports DataType=Number for a cell whose *format* is a
            // built-in date format (e.g. NumberFormatId 17 = "mmm-yy") instead of DataType=DateTime
            // — GetDateTime() then throws on it. Detect that case by format rather than DataType.
            if (cell.DataType == XLDataType.Number && IsDateFormatted(cell))
                return DateTime.FromOADate(cell.GetDouble()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // A cell formatted as a percentage (e.g. displaying "6.36%") stores its RAW fraction
            // (0.0636) — Excel only multiplies by 100 at display time. Without this, every
            // percentage-based field in the summary DTOs (GWM ratios, RIM, LDR, AL/DPK, AL/NCD, ...)
            // came back 100x too small (e.g. "0.0636" instead of "6.36").
            if (cell.DataType == XLDataType.Number && IsPercentFormatted(cell))
                return (cell.GetDouble() * 100).ToString(CultureInfo.InvariantCulture);

            return cell.DataType switch
            {
                XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
                XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                XLDataType.Boolean => cell.GetBoolean().ToString(CultureInfo.InvariantCulture),
                _ => cell.GetString().Trim()
            };
        }

        private static bool IsDateFormatted(IXLCell cell)
        {
            var id = cell.Style.NumberFormat.NumberFormatId;
            if (id is >= 14 and <= 22) return true; // built-in Excel date/time formats

            var fmt = cell.Style.NumberFormat.Format;
            if (string.IsNullOrEmpty(fmt)) return false;
            var withoutLiterals = Regex.Replace(fmt, "\"[^\"]*\"", "");
            return !withoutLiterals.Contains('%') && Regex.IsMatch(withoutLiterals, "[dmyhs]", RegexOptions.IgnoreCase);
        }

        private static bool IsPercentFormatted(IXLCell cell)
        {
            var id = cell.Style.NumberFormat.NumberFormatId;
            if (id is 9 or 10) return true; // built-in Excel percent formats ("0%", "0.00%")

            var fmt = cell.Style.NumberFormat.Format;
            if (string.IsNullOrEmpty(fmt)) return false;
            var withoutLiterals = Regex.Replace(fmt, "\"[^\"]*\"", "");
            return withoutLiterals.Contains('%');
        }

        // For "status" cells built as a number with a custom display format carrying a literal
        // suffix (e.g. format `0.00%\ "OVER"` on a plain numeric cell — ClosedXML's GetString()
        // can't render that format and just returns the corrupted raw number). The suffix here is
        // a single, unconditional format section (no ";"-separated branches), so it's the same
        // literal text for every value in the column — reproducing it is not a business-rule
        // guess, just replaying what Excel itself would display.
        private static string GetCellStatusText(IXLCell cell)
        {
            if (cell.IsEmpty()) return string.Empty;
            if (cell.DataType != XLDataType.Number) return GetCellText(cell);

            var fmt = cell.Style.NumberFormat.Format;
            var literal = string.Join(" ", Regex.Matches(fmt, "\"([^\"]*)\"").Select(m => m.Groups[1].Value)).Trim();
            if (literal.Length == 0) return GetCellText(cell);

            var val = cell.GetDouble();
            var numberPart = fmt.Contains('%')
                ? (val * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                : val.ToString(CultureInfo.InvariantCulture);
            return $"{numberPart} {literal}";
        }

        // =====================================================================
        // 1. KAJIAN RISIKO LIKUIDITAS
        // =====================================================================

        public async Task<(bool Success, KajianRisikoExtractResponseDto Result)> ExtractKajianRisikoAsync(IFormFile file, CancellationToken ct)
        {
            var response = new KajianRisikoExtractResponseDto { Message = "Gagal memproses file." };

            if (file == null || file.Length == 0) return (false, response);
            if (!IsExcelFile(file)) { response.Message = "Format file harus .xlsx atau .xls"; return (false, response); }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);

                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(UploadSheetName, StringComparison.OrdinalIgnoreCase));

                if (ws == null) { response.Message = $"Sheet '{UploadSheetName}' tidak ditemukan."; return (false, response); }

                var lastRowUsed = ws.LastRowUsed();
                if (lastRowUsed == null) { response.Message = "Sheet 'upload' kosong tidak ada data."; return (true, response); }

                response.Summary.Periode = GetCellText(ws.Cell(1, 5));

                int maxRow = lastRowUsed.RowNumber();
                var specs = BuildKajianFieldSpecs();
                var specIndex = 0;

                for (int row = 2; row <= maxRow; row++)
                {
                    var colA = ws.Cell(row, 1).GetString().Trim();
                    var colB = ws.Cell(row, 2).GetString().Trim();
                    var colC = ws.Cell(row, 3).GetString().Trim();
                    var nilai = GetCellText(ws.Cell(row, 5));
                    var status = GetCellStatusText(ws.Cell(row, 6));

                    if (colA.Length == 0 && colB.Length == 0 && colC.Length == 0) continue; // fully blank row

                    // The table's only section-header rows are "A." through "H." (col A). Anything
                    // else landing in col A — "Catatan:", asterisk-marked footnotes, stray formula
                    // text left below the table — means we've walked off the bottom of the real
                    // table, so stop rather than extracting notes as if they were data rows.
                    if (colA.Length > 0 && !Regex.IsMatch(colA, @"^[A-H]\.$")) break;

                    string keterangan;
                    int indent;

                    if (colA.Length > 0)
                    {
                        keterangan = colB; // section header: label sits in column B
                        indent = 0;
                    }
                    else if (colB.Length > 0)
                    {
                        keterangan = colC; // numbered item ("1.", "2." in col B, label in col C)
                        indent = 1;
                    }
                    else if (Regex.IsMatch(colC, @"^[a-cA-C]\.\s"))
                    {
                        keterangan = colC;
                        indent = 2;
                    }
                    else if (colC.TrimStart().StartsWith("▫"))
                    {
                        keterangan = colC;
                        indent = 3;
                    }
                    else
                    {
                        keterangan = colC;
                        indent = 2;
                    }

                    response.Rows.Add(new KajianRisikoRowDto
                    {
                        SectionCode = colA.Length > 0 ? colA : null,
                        SubNumber = colB.Length > 0 && colA.Length == 0 ? colB : null,
                        Keterangan = keterangan,
                        Nilai = nilai.Length > 0 ? nilai : null,
                        Status = status.Length > 0 ? status : null,
                        IndentLevel = indent
                    });

                    if (specIndex >= specs.Count) continue;

                    var normalized = Normalize(keterangan);
                    var current = specs[specIndex];

                    if (current.Matches(normalized))
                    {
                        current.Apply(response.Summary, nilai, status);
                        specIndex++;
                    }
                }

                response.UnmatchedFields = specs.Skip(specIndex).Select(s => s.FieldName).ToList();
                response.TotalRows = response.Rows.Count;
                response.Message = response.UnmatchedFields.Count == 0
                    ? "Berhasil ekstrak Kajian Risiko (seluruh field cocok)."
                    : $"Berhasil ekstrak Kajian Risiko. {response.UnmatchedFields.Count} field tidak ditemukan pada sheet (kemungkinan format sheet berubah): {string.Join(", ", response.UnmatchedFields)}.";

                return (true, response);
            }
            catch (Exception ex)
            {
                response.Message = $"Terjadi kesalahan saat memproses file: {ex.Message}";
                return (false, response);
            }
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            t = Regex.Replace(t, @"^▫\s*", "");
            t = Regex.Replace(t, @"^[a-cA-C]\.\s*", "");
            t = Regex.Replace(t, @"\*+\s*$", "");
            t = Regex.Replace(t, @"\s+", " ").Trim();
            return t.ToLowerInvariant();
        }

        private static decimal? ParseDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var t = raw.Trim();
            if (t == "-" || t.Length == 0) return null;

            if (t.EndsWith("%")) t = t[..^1].Trim();

            t = t.Replace(",", "").Replace(" ", "");

            return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : null;
        }

        private class KajianFieldSpec
        {
            public string FieldName { get; init; } = string.Empty;
            public Func<string, bool> Matches { get; init; } = _ => false;
            public Action<KajianRisikoSummaryDto, string?, string?> Apply { get; init; } = (_, __, ___) => { };
        }

        // Ordered exactly as the fields appear top-to-bottom in the "upload" sheet (PRD §7.3.1
        // sections A-H). Sequential + normalized-text matching disambiguates the several labels
        // that repeat verbatim (e.g. "Kas Rupiah (Juta Rupiah)" is both a Kondisi row and a value
        // row a few lines later) without depending on fragile absolute row numbers.
        private static List<KajianFieldSpec> BuildKajianFieldSpecs() => new()
        {
            Spec("KondisiKasRupiah", t => t == "kas rupiah (juta rupiah)", (s, n, st) => s.KondisiKasRupiah = n),
            Spec("PaguKasRupiahBniWide", t => t.StartsWith("pagu kas rupiah bni wide"), (s, n, st) => s.PaguKasRupiahBniWide = ParseDecimal(n)),
            Spec("KasRupiah", t => t == "kas rupiah (juta rupiah)", (s, n, st) => { s.KasRupiah = ParseDecimal(n); s.PersenTerhadapPaguRupiah = ParseDecimal(st); }),
            Spec("RataRataRealisasiKasRupiah", t => t.StartsWith("rata-rata realisasi kas rupiah"), (s, n, st) => { s.RataRataRealisasiKasRupiah = ParseDecimal(n); s.TrafficLightKasRupiah = st; }),
            Spec("GwmHarianRupiah", t => t.StartsWith("gwm harian"), (s, n, st) => { s.GwmHarianRupiah = ParseDecimal(n); s.GwmHarianRupiahStatus = st; }),
            Spec("RcBiRupiah", t => t == "r/c bi", (s, n, st) => s.RcBiRupiah = ParseDecimal(n)),
            Spec("DpkRupiah", t => t == "dpk", (s, n, st) => s.DpkRupiah = ParseDecimal(n)),
            Spec("GwmAveragingRupiah", t => t.StartsWith("gwm averaging"), (s, n, st) => { s.GwmAveragingRupiah = ParseDecimal(n); s.GwmAveragingRupiahStatus = st; }),
            Spec("PemenuhanGwmRupiah", t => t.StartsWith("pemenuhan gwm"), (s, n, st) => s.PemenuhanGwmRupiah = ParseDecimal(n)),
            Spec("ExcessReserveRupiah", t => t.StartsWith("excess reserve"), (s, n, st) => s.ExcessReserveRupiah = ParseDecimal(n)),
            Spec("GwmSekunderPlmRupiah", t => t.StartsWith("gwm sekunder (plm)"), (s, n, st) => { s.GwmSekunderPlmRupiah = ParseDecimal(n); s.GwmSekunderPlmRupiahStatus = st; }),
            Spec("SbiSdbiSbnRupiah", t => t.StartsWith("sbi, sdbi, dan sbn"), (s, n, st) => s.SbiSdbiSbnRupiah = ParseDecimal(n)),
            Spec("KetentuanGwmRupiah", t => t.StartsWith("gwm rupiah (padg"), (s, n, st) => s.KetentuanGwmRupiah = ParseDecimal(n)),
            Spec("KetentuanGwmSekunderPlmRupiah", t => t.StartsWith("gwm sekunder - plm"), (s, n, st) => s.KetentuanGwmSekunderPlmRupiah = ParseDecimal(n)),

            Spec("KasValas", t => t.StartsWith("kas valas (ribu usd)"), (s, n, st) => s.KasValas = ParseDecimal(n)),
            Spec("PaguKasValasBniWide", t => t.StartsWith("pagu kas valas bni wide"), (s, n, st) => { s.PaguKasValasBniWide = ParseDecimal(n); s.PersenTerhadapPaguValas = ParseDecimal(st); }),
            Spec("RataRataRealisasiKasValas", t => t.StartsWith("rata-rata realisasi kas valas"), (s, n, st) => { s.RataRataRealisasiKasValas = ParseDecimal(n); s.TrafficLightKasValas = st; }),
            Spec("GwmValas", t => t == "gwm valas", (s, n, st) => { s.GwmValas = ParseDecimal(n); s.GwmValasStatus = st; }),
            Spec("RcBiValas", t => t == "r/c bi valas", (s, n, st) => s.RcBiValas = ParseDecimal(n)),
            Spec("DpkValas", t => t == "dpk valas", (s, n, st) => s.DpkValas = ParseDecimal(n)),
            Spec("KetentuanGwmValas", t => t == "gwm valas", (s, n, st) => s.KetentuanGwmValas = ParseDecimal(n)),

            Spec("TightNormalRupiah", t => t.StartsWith("tight/normal"), (s, n, st) => s.TightNormalRupiah = n),
            Spec("SafetyLevelRupiah", t => t == "safety level", (s, n, st) => s.SafetyLevelRupiah = ParseDecimal(n)),
            Spec("CadanganLikuiditasRupiah", t => t.StartsWith("cadangan likuiditas"), (s, n, st) => { s.CadanganLikuiditasRupiah = ParseDecimal(n); s.TrafficLightCadanganRupiah = st; }),
            Spec("TrLiquidRupiah", t => t.StartsWith("tr liquid"), (s, n, st) => s.TrLiquidRupiah = ParseDecimal(n)),
            Spec("StatusLikuiditasRupiah", t => t.StartsWith("status likuiditas"), (s, n, st) => s.StatusLikuiditasRupiah = n),

            Spec("TightNormalValas", t => t.StartsWith("tight/normal"), (s, n, st) => s.TightNormalValas = n),
            Spec("SafetyLevelValas", t => t == "safety level", (s, n, st) => s.SafetyLevelValas = ParseDecimal(n)),
            Spec("CadanganLikuiditasValas", t => t.StartsWith("cadangan likuiditas"), (s, n, st) => { s.CadanganLikuiditasValas = ParseDecimal(n); s.TrafficLightCadanganValas = st; }),
            Spec("TrLiquidValas", t => t.StartsWith("tr liquid"), (s, n, st) => s.TrLiquidValas = ParseDecimal(n)),

            Spec("RimKredit", t => t == "kredit", (s, n, st) => s.RimKredit = ParseDecimal(n)),
            Spec("RimSuratBerhargaDimiliki", t => t.StartsWith("surat berharga yang dimiliki"), (s, n, st) => s.RimSuratBerhargaDimiliki = ParseDecimal(n)),
            Spec("RimWeselEkspor", t => t.StartsWith("wesel ekspor"), (s, n, st) => s.RimWeselEkspor = ParseDecimal(n)),
            Spec("RimDpk", t => t == "dpk", (s, n, st) => s.RimDpk = ParseDecimal(n)),
            Spec("RimPinjamanYangDiterima", t => t.StartsWith("pinjaman yang diterima"), (s, n, st) => s.RimPinjamanYangDiterima = ParseDecimal(n)),
            Spec("RimSuratBerhargaDiterbitkan", t => t.StartsWith("surat berharga yang diterbitkan"), (s, n, st) => s.RimSuratBerhargaDiterbitkan = ParseDecimal(n)),
            Spec("RimPercent", t => t.StartsWith("rim (risk appetite"), (s, n, st) => s.RimPercent = ParseDecimal(n)),
            Spec("RimPosisi", t => t.StartsWith("informasi posisi rim"), (s, n, st) => s.RimPosisi = n),
            Spec("RimDisinsentif", t => t.StartsWith("disinsentif rim"), (s, n, st) => s.RimDisinsentif = ParseDecimal(n)),

            Spec("InsentifKlm", t => t.StartsWith("surat bi terkait"), (s, n, st) => s.InsentifKlm = ParseDecimal(n)),

            Spec("LdrRupiah", t => t.StartsWith("ldr rupiah"), (s, n, st) => s.LdrRupiah = ParseDecimal(n)),
            Spec("LdrValas", t => t.StartsWith("ldr valas"), (s, n, st) => s.LdrValas = ParseDecimal(n)),
            Spec("LdrTotal", t => t.StartsWith("ldr total"), (s, n, st) => s.LdrTotal = ParseDecimal(n)),

            Spec("AlNcd", t => t.StartsWith("al/ncd"), (s, n, st) => s.AlNcd = ParseDecimal(n)),
            Spec("AlDpk", t => t.StartsWith("al/dpk"), (s, n, st) => s.AlDpk = ParseDecimal(n)),
        };

        private static KajianFieldSpec Spec(string name, Func<string, bool> matches, Action<KajianRisikoSummaryDto, string?, string?> apply)
            => new() { FieldName = name, Matches = matches, Apply = apply };

        // =====================================================================
        // 2. PROFIL MATURITAS & RESERVE REQUIREMENT KLN
        // =====================================================================

        public async Task<(bool Success, ExtractResponseDto<ProfilMaturitasKlnDto> Result)> ExtractProfilMaturitasKlnAsync(IFormFile file, CancellationToken ct)
        {
            var response = new ExtractResponseDto<ProfilMaturitasKlnDto>
            {
                Message = "Gagal memproses file.",
                Data = new List<ProfilMaturitasKlnDto>()
            };

            if (file == null || file.Length == 0) return (false, response);
            if (!IsExcelFile(file)) { response.Message = "Format file harus .xlsx atau .xls"; return (false, response); }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);

                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(UploadSheetName, StringComparison.OrdinalIgnoreCase));

                if (ws == null) { response.Message = $"Sheet '{UploadSheetName}' tidak ditemukan."; return (false, response); }

                var lastRowUsed = ws.LastRowUsed();
                if (lastRowUsed == null) { response.Message = "Sheet 'upload' kosong."; return (true, response); }

                response.Periode = GetCellText(ws.Cell(1, 1));

                int maxRow = lastRowUsed.RowNumber();
                for (int row = 2; row <= maxRow; row++)
                {
                    var cabang = ws.Cell(row, 1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(cabang)) continue;

                    response.Data.Add(new ProfilMaturitasKlnDto
                    {
                        Cabang = cabang,
                        Aset = ParseDouble(GetCellText(ws.Cell(row, 2))),
                        Kewajiban = ParseDouble(GetCellText(ws.Cell(row, 3))),
                        Selisih = ParseDouble(GetCellText(ws.Cell(row, 4))),
                        ProfilMaturitasPercent = ParseDouble(GetCellText(ws.Cell(row, 5))),
                        ReserveRequirement = NullIfEmpty(ws.Cell(row, 6).GetString()),
                        TrafficLight = NullIfEmpty(ws.Cell(row, 7).GetString())
                    });
                }

                response.TotalRows = response.Data.Count;
                response.Message = "Berhasil ekstrak Profil Maturitas KLN.";
                return (true, response);
            }
            catch (Exception ex)
            {
                response.Message = $"Terjadi kesalahan saat memproses file: {ex.Message}";
                return (false, response);
            }
        }

        private static double ParseDouble(string? raw) => (double)(ParseDecimal(raw) ?? 0);

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // =====================================================================
        // 3. RESUME MATURITY PROFILE HO (Contractual / Behavioral)
        // =====================================================================

        public async Task<(bool Success, ResumeMatProfHoExtractResponseDto Result)> ExtractResumeMatProfHoAsync(IFormFile file, CancellationToken ct, string? preferredSheet = null)
        {
            var response = new ResumeMatProfHoExtractResponseDto { Message = "Gagal memproses file." };

            if (file == null || file.Length == 0) return (false, response);
            if (!IsExcelFile(file)) { response.Message = "Format file harus .xlsx atau .xls"; return (false, response); }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);

                using var workbook = new XLWorkbook(stream);

                // A real workbook carries BOTH "Kontraktual" and "Behavioral" sheets side by side
                // (they are not alternates). Without an explicit preference the old code always
                // resolved to "upload", then "Kontraktual" — "Behavioral" was unreachable whenever
                // a "Kontraktual" sheet also existed in the same file, even if the caller wanted
                // Behavioral data specifically.
                IXLWorksheet? ws = null;
                string tipe = "Upload";

                if (!string.IsNullOrWhiteSpace(preferredSheet))
                {
                    ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(preferredSheet, StringComparison.OrdinalIgnoreCase));
                    if (ws != null) tipe = preferredSheet.Trim();
                }

                if (ws == null)
                {
                    ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(UploadSheetName, StringComparison.OrdinalIgnoreCase));
                    tipe = "Upload";
                }

                if (ws == null)
                {
                    // Fallback for callers that didn't specify a preference: Kontraktual first, then Behavioral.
                    ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("Kontraktual", StringComparison.OrdinalIgnoreCase));
                    tipe = "Kontraktual";

                    if (ws == null)
                    {
                        ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("Behavioral", StringComparison.OrdinalIgnoreCase));
                        tipe = "Behavioral";
                    }
                }

                if (ws == null)
                {
                    response.Message = $"Sheet '{UploadSheetName}' (atau 'Kontraktual'/'Behavioral') tidak ditemukan.";
                    return (false, response);
                }

                var lastRowUsed = ws.LastRowUsed();
                if (lastRowUsed == null) { response.Message = $"Sheet '{ws.Name}' kosong."; return (true, response); }

                response.Summary.Tipe = tipe;
                response.Summary.Periode = GetCellText(ws.Cell(1, 5));

                int maxRow = lastRowUsed.RowNumber();
                for (int row = 2; row <= maxRow; row++)
                {
                    var kategori = ws.Cell(row, 1).GetString().Trim();
                    var sandi = ws.Cell(row, 3).GetString().Trim();
                    var idrRaw = GetCellText(ws.Cell(row, 5));
                    var vaRaw = GetCellText(ws.Cell(row, 6));

                    // Keep any row that carries a label OR a sandi code — the old filter dropped
                    // "D. Kumulatif", "Selisih Kumulatif", "Gap Maturitas" and "Traffic Light"
                    // entirely because none of them have a Sandi value.
                    if (kategori.Length == 0 && sandi.Length == 0) continue;

                    var nilaiIdr = ParseDecimal(idrRaw);
                    var nilaiVa = ParseDecimal(vaRaw);

                    response.Rows.Add(new ResumeMatProfHoDto
                    {
                        KategoriNeraca = kategori.Length > 0 ? kategori : null,
                        Sandi = sandi.Length > 0 ? sandi : null,
                        NilaiIdr = (double?)nilaiIdr,
                        NilaiVa = (double?)nilaiVa
                    });

                    ApplyResumeField(response.Summary, kategori, sandi, idrRaw, vaRaw, nilaiIdr, nilaiVa);
                }

                response.TotalRows = response.Rows.Count;
                response.Message = $"Berhasil ekstrak Resume MatProf HO ({tipe}).";
                return (true, response);
            }
            catch (Exception ex)
            {
                response.Message = $"Terjadi kesalahan saat memproses file: {ex.Message}";
                return (false, response);
            }
        }

        private static void ApplyResumeField(ResumeMatProfHoSummaryDto s, string kategori, string sandi, string idrRaw, string vaRaw, decimal? idr, decimal? va)
        {
            switch (sandi)
            {
                case "10000": s.AssetIdr = idr; s.AssetVa = va; return;
                case "20000": s.KewajibanIdr = idr; s.KewajibanVa = va; return;
                case "30000": s.SelisihNeracaIdr = idr; s.SelisihNeracaVa = va; return;
                case "40000": s.TagihanRekAdmIdr = idr; s.TagihanRekAdmVa = va; return;
                case "50000": s.KewajibanRekAdmIdr = idr; s.KewajibanRekAdmVa = va; return;
                case "60000": s.SelisihRekAdmIdr = idr; s.SelisihRekAdmVa = va; return;
                case "70000": s.SelisihGabunganIdr = idr; s.SelisihGabunganVa = va; return;
                case "80000": s.SelisihKumulatifIdr = idr; s.SelisihKumulatifVa = va; return;
            }

            var normalized = Normalize(kategori);

            if (normalized.StartsWith("d. kumulatif")) { s.KumulatifVa = va; return; }
            if (normalized.StartsWith("gap maturitas")) { s.GapMaturitasIdr = idr; s.GapMaturitasVa = va; return; }
            if (normalized.StartsWith("traffict light") || normalized.StartsWith("traffic light"))
            {
                // Not applicable rows are literally "-" in the source (Contractual sheet) — keep those as null.
                s.TrafficLightIdr = idrRaw == "-" ? null : NullIfEmpty(idrRaw);
                s.TrafficLightVa = vaRaw == "-" ? null : NullIfEmpty(vaRaw);
            }
        }

        // =====================================================================

    }
}
