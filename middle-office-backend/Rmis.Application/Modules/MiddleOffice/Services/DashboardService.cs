using Microsoft.AspNetCore.Http;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Dtos;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace middle_office_backend.Rmis.Application.Modules.MiddleOffice.Services
{
    // Builds chart/card-ready dashboard payloads from a batch of uploaded workbooks. Stateless:
    // there is no database yet, so "history" is exactly the set of files the caller uploads in one
    // request — one file per period they want plotted. Once persistence lands, a future version of
    // this service can source the same (Periode, Summary) pairs from the DB instead of from files
    // uploaded in-request; the chart-building logic below (grouping, sorting, delta calc) carries
    // over unchanged.
    public class DashboardService : IDashboardService
    {
        private readonly IMiddleOfficeServices _extractor;

        public DashboardService(IMiddleOfficeServices extractor)
        {
            _extractor = extractor;
        }

        // A metric is either chartable (Selector, a decimal time series — GWM ratios, Rupiah/USD
        // amounts, ...) or status-only (TextSelector — Kondisi, Traffic Light, Posisi, Tight/Normal).
        // Status metrics get a card but never a chart series: there's no meaningful line to draw
        // through "Normal" / "DARK GREEN" / "Dalam Range Risk Appetite".
        private record MetricDef(string Key, string Label, Func<KajianRisikoSummaryDto, decimal?>? Selector, Func<KajianRisikoSummaryDto, string?>? TextSelector = null);

        // 12 sub-categories grouped under the sheet's own 8 lettered sections (A-H) — verified
        // field-by-field against the PowerPoint mockup's 12 "PERIODIC DATE" dashboard slides, not an
        // arbitrary regrouping. E (RIM) alone splits into 3 (Kredit / DPK / Rasio) because the mockup
        // dedicates a separate slide to each of those three, same for A and B (Kas vs GWM raw amounts).
        private record ChartDef(string Key, string Title, string Group, string GroupLabel, string? Unit, List<MetricDef> Metrics);

        private static readonly List<ChartDef> KajianRisikoCharts = new()
        {
            new("kasRupiah", "Kas Rupiah", "A", "A. Primary Reserve Rupiah", "Juta Rupiah", new()
            {
                new("kasRupiah", "Kas Rupiah", s => s.KasRupiah, null),
                new("paguKasRupiahBniWide", "Pagu Kas Rupiah BNI Wide", s => s.PaguKasRupiahBniWide, null),
                new("rataRataRealisasiKasRupiah", "Rata-rata Realisasi Kas Rupiah", s => s.RataRataRealisasiKasRupiah, null),
                new("kondisiKasRupiah", "Kondisi", null, s => s.KondisiKasRupiah),
            }),
            new("gwmRupiah", "GWM Rupiah", "A", "A. Primary Reserve Rupiah", "Juta Rupiah", new()
            {
                new("rcBiRupiah", "R/C BI", s => s.RcBiRupiah, null),
                new("dpkRupiah", "DPK", s => s.DpkRupiah, null),
                new("excessReserveRupiah", "Excess Reserve", s => s.ExcessReserveRupiah, null),
                new("sbiSdbiSbnRupiah", "SBI, SDBI, dan SBN", s => s.SbiSdbiSbnRupiah, null),
            }),
            new("kasValas", "Kas Valas", "B", "B. Primary Reserve Valas", "Ribu USD", new()
            {
                new("kasValas", "Kas Valas", s => s.KasValas, null),
                new("paguKasValasBniWide", "Pagu Kas Valas BNI Wide", s => s.PaguKasValasBniWide, null),
                new("rataRataRealisasiKasValas", "Rata-rata Realisasi Kas Valas", s => s.RataRataRealisasiKasValas, null),
                new("trafficLightKasValas", "Traffic Light", null, s => s.TrafficLightKasValas),
            }),
            new("gwmValas", "GWM Valas", "B", "B. Primary Reserve Valas", null, new()
            {
                new("rcBiValas", "R/C BI Valas", s => s.RcBiValas, null),
                new("dpkValas", "DPK Valas", s => s.DpkValas, null),
                new("gwmValas", "GWM Valas", s => s.GwmValas, null),
            }),
            new("safetyLevelRupiah", "Safety Level Rupiah", "C", "C. Safety Level Rupiah", "Juta Rupiah", new()
            {
                new("tightNormalRupiah", "Tight/Normal", null, s => s.TightNormalRupiah),
                new("trLiquidRupiah", "TR Liquid", s => s.TrLiquidRupiah, null),
                new("cadanganLikuiditasRupiah", "Cadangan Likuiditas", s => s.CadanganLikuiditasRupiah, null),
                new("safetyLevelRupiah", "Safety Level", s => s.SafetyLevelRupiah, null),
            }),
            new("safetyLevelValas", "Safety Level Valas", "D", "D. Safety Level Valas", "Ribu USD", new()
            {
                new("tightNormalValas", "Tight/Normal", null, s => s.TightNormalValas),
                new("trLiquidValas", "TR Liquid", s => s.TrLiquidValas, null),
                new("cadanganLikuiditasValas", "Cadangan Likuiditas", s => s.CadanganLikuiditasValas, null),
                new("safetyLevelValas", "Safety Level", s => s.SafetyLevelValas, null),
            }),
            new("rimKredit", "RIM - Kredit", "E", "E. Rasio Intermediasi Makroprudensial (RIM)", "Juta Rupiah", new()
            {
                new("rimKredit", "Kredit", s => s.RimKredit, null),
                new("rimWeselEkspor", "Wesel Ekspor", s => s.RimWeselEkspor, null),
                new("rimSuratBerhargaDimiliki", "Surat Berharga yang Dimiliki", s => s.RimSuratBerhargaDimiliki, null),
            }),
            new("rimDpk", "RIM - DPK", "E", "E. Rasio Intermediasi Makroprudensial (RIM)", "Juta Rupiah", new()
            {
                new("rimDpk", "DPK", s => s.RimDpk, null),
                new("rimSuratBerhargaDiterbitkan", "Surat Berharga yang Diterbitkan", s => s.RimSuratBerhargaDiterbitkan, null),
                new("rimPinjamanYangDiterima", "Pinjaman yang Diterima", s => s.RimPinjamanYangDiterima, null),
            }),
            new("rimRasio", "RIM - Rasio", "E", "E. Rasio Intermediasi Makroprudensial (RIM)", "%", new()
            {
                new("rimPercent", "RIM", s => s.RimPercent, null),
                new("rimPosisi", "Posisi RIM", null, s => s.RimPosisi),
                new("rimDisinsentif", "Disinsentif RIM", s => s.RimDisinsentif, null),
            }),
            new("insentifKlm", "Insentif KLM", "F", "F. Insentif KLM", "%", new()
            {
                new("insentifKlm", "Insentif KLM", s => s.InsentifKlm, null),
            }),
            new("ldr", "Loan to Deposit Ratio", "G", "G. Loan to Deposit Ratio (LDR)", "%", new()
            {
                new("ldrRupiah", "LDR Rupiah", s => s.LdrRupiah, null),
                new("ldrValas", "LDR Valas", s => s.LdrValas, null),
                new("ldrTotal", "LDR Total", s => s.LdrTotal, null),
            }),
            new("alRatio", "AL/DPK & AL/NCD", "H", "H. AL:DPK dan AL:NCD", "%", new()
            {
                new("alNcd", "AL/NCD", s => s.AlNcd, null),
                new("alDpk", "AL/DPK", s => s.AlDpk, null),
            }),
        };

        public async Task<DashboardResponseDto> BuildKajianRisikoDashboardAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
        {
            var response = new DashboardResponseDto { PeriodsRequested = files.Count };
            var points = new List<(string Periode, KajianRisikoSummaryDto Summary)>();

            foreach (var file in files)
            {
                var (success, result) = await _extractor.ExtractKajianRisikoAsync(file, ct);
                if (!success)
                {
                    response.Warnings.Add($"{file.FileName}: {result.Message}");
                    continue;
                }
                points.Add((result.Summary.Periode ?? file.FileName, result.Summary));
            }

            var ordered = OrderByPeriode(points);
            response.PeriodsProcessed = ordered.Count;
            response.Message = ordered.Count > 0
                ? $"Berhasil membangun dashboard dari {ordered.Count} periode."
                : "Tidak ada periode yang berhasil diekstrak.";

            foreach (var chartDef in KajianRisikoCharts)
            {
                response.Charts.Add(BuildChart(chartDef, ordered));
                foreach (var metric in chartDef.Metrics)
                    response.Cards.Add(BuildCard(chartDef.Key, metric, chartDef.Unit, ordered));
            }

            return response;
        }

        public async Task<DashboardResponseDto> BuildResumeHoDashboardAsync(IReadOnlyList<IFormFile> files, string sheetTipe, CancellationToken ct)
        {
            var response = new DashboardResponseDto { PeriodsRequested = files.Count };
            var points = new List<(string Periode, ResumeMatProfHoSummaryDto Summary)>();

            foreach (var file in files)
            {
                var (success, result) = await _extractor.ExtractResumeMatProfHoAsync(file, ct, sheetTipe);
                if (!success)
                {
                    response.Warnings.Add($"{file.FileName}: {result.Message}");
                    continue;
                }
                points.Add((result.Summary.Periode ?? file.FileName, result.Summary));
            }

            var ordered = OrderByPeriode(points);
            response.PeriodsProcessed = ordered.Count;
            response.Message = ordered.Count > 0
                ? $"Berhasil membangun dashboard {sheetTipe} dari {ordered.Count} periode."
                : "Tidak ada periode yang berhasil diekstrak.";

            List<(string Key, string Label, Func<ResumeMatProfHoSummaryDto, decimal?> Selector)> idrMetrics = new()
            {
                ("assetIdr", "Asset IDR", s => s.AssetIdr),
                ("kewajibanIdr", "Kewajiban IDR", s => s.KewajibanIdr),
                ("selisihNeracaIdr", "Selisih Neraca IDR", s => s.SelisihNeracaIdr),
            };
            List<(string Key, string Label, Func<ResumeMatProfHoSummaryDto, decimal?> Selector)> vaMetrics = new()
            {
                ("assetVa", "Asset VA", s => s.AssetVa),
                ("kewajibanVa", "Kewajiban VA", s => s.KewajibanVa),
                ("selisihNeracaVa", "Selisih Neraca VA", s => s.SelisihNeracaVa),
            };

            response.Charts.Add(BuildChart("neracaIdr", "Neraca IDR", "IDR", idrMetrics, ordered));
            response.Charts.Add(BuildChart("neracaVa", "Neraca VA", "VA", vaMetrics, ordered));

            foreach (var m in idrMetrics.Concat(vaMetrics))
                response.Cards.Add(BuildCard(m.Key, m.Label, null, ordered, m.Selector));

            return response;
        }

        public async Task<DashboardResponseDto> BuildProfilKlnDashboardAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
        {
            var response = new DashboardResponseDto { PeriodsRequested = files.Count };
            var points = new List<(string Periode, ExtractResponseDto<ProfilMaturitasKlnDto> Result)>();

            foreach (var file in files)
            {
                var (success, result) = await _extractor.ExtractProfilMaturitasKlnAsync(file, ct);
                if (!success)
                {
                    response.Warnings.Add($"{file.FileName}: {result.Message}");
                    continue;
                }
                points.Add((result.Periode ?? file.FileName, result));
            }

            var ordered = points
                .Select(p => (p.Periode, SortKey: NormalizePeriodeForSort(p.Periode), p.Result))
                .OrderBy(p => p.SortKey, StringComparer.Ordinal)
                .ToList();

            response.PeriodsProcessed = ordered.Count;
            response.Message = ordered.Count > 0
                ? $"Berhasil membangun dashboard Profil KLN dari {ordered.Count} periode."
                : "Tidak ada periode yang berhasil diekstrak.";

            // One line per branch (branches are data-driven, not a fixed field list like the other reports).
            var branchNames = ordered
                .SelectMany(p => p.Result.Data.Select(d => d.Cabang))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var chart = new LineChartDto { Key = "profilMaturitasKln", Title = "Profil Maturitas % per Cabang", Unit = "%" };
            foreach (var branch in branchNames)
            {
                var series = new LineChartSeriesDto { Key = Slugify(branch), Label = branch };
                foreach (var (periode, sortKey, result) in ordered)
                {
                    var row = result.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase));
                    series.Points.Add(new LineChartPointDto
                    {
                        Period = periode,
                        SortKey = sortKey,
                        Value = row != null ? (decimal)row.ProfilMaturitasPercent : null
                    });
                }
                chart.Series.Add(series);
            }
            response.Charts.Add(chart);

            // One chart per branch (Key/Group = the branch slug, matching that branch's cards'
            // Category) carrying the full per-period history for every numeric metric — not just
            // Profil Maturitas % — so the frontend can render a per-period table per branch instead
            // of only ever showing the latest period.
            foreach (var branch in branchNames)
            {
                var category = Slugify(branch);
                var branchChart = new LineChartDto { Key = category, Title = branch, Group = "branch", GroupLabel = "Cabang" };
                branchChart.Series.Add(BuildBranchSeries("aset", "Aset", ordered, branch, d => d.Aset));
                branchChart.Series.Add(BuildBranchSeries("kewajiban", "Kewajiban", ordered, branch, d => d.Kewajiban));
                branchChart.Series.Add(BuildBranchSeries("selisih", "Selisih", ordered, branch, d => d.Selisih));
                branchChart.Series.Add(BuildBranchSeries("profilMaturitasPercent", "Profil Maturitas", ordered, branch, d => d.ProfilMaturitasPercent));
                response.Charts.Add(branchChart);
            }

            // Summary cards from the latest period: totals + how many branches are NOT OK.
            // Category is left empty — these describe all branches combined, not one specific branch.
            var latest = ordered.LastOrDefault();
            if (latest.Result != null)
            {
                var latestData = latest.Result.Data;
                response.Cards.Add(new MetricCardDto
                {
                    Key = "totalAset",
                    Label = "Total Aset (semua cabang)",
                    LatestValue = (decimal)latestData.Sum(d => d.Aset),
                    LatestPeriod = latest.Periode
                });
                response.Cards.Add(new MetricCardDto
                {
                    Key = "totalKewajiban",
                    Label = "Total Kewajiban (semua cabang)",
                    LatestValue = (decimal)latestData.Sum(d => d.Kewajiban),
                    LatestPeriod = latest.Periode
                });
                response.Cards.Add(new MetricCardDto
                {
                    Key = "cabangNotOk",
                    Label = "Cabang Reserve Requirement NOT OK",
                    LatestValue = latestData.Count(d => !string.Equals(d.ReserveRequirement, "OK", StringComparison.OrdinalIgnoreCase)),
                    LatestPeriod = latest.Periode
                });
            }

            // Per-branch cards (Category = the same slug as the branch's chart series key) so the
            // frontend's branch checkboxes can pick exactly which branch-cards to show, the same way
            // the Kajian Risiko category select picks a Category out of its cards.
            foreach (var branch in branchNames)
            {
                var category = Slugify(branch);
                AddBranchCard(response, category, "aset", "Aset", ordered, r => r.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase))?.Aset is double a ? (decimal)a : null);
                AddBranchCard(response, category, "kewajiban", "Kewajiban", ordered, r => r.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase))?.Kewajiban is double k ? (decimal)k : null);
                AddBranchCard(response, category, "selisih", "Selisih", ordered, r => r.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase))?.Selisih is double s ? (decimal)s : null);
                AddBranchCard(response, category, "profilMaturitasPercent", "Profil Maturitas", ordered, r => r.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase))?.ProfilMaturitasPercent is double p ? (decimal)p : null, unit: "%");

                var latestRow = latest.Result?.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase));
                var previousRow = ordered.Count >= 2 ? ordered[^2].Result.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase)) : null;
                response.Cards.Add(new MetricCardDto
                {
                    Key = "reserveRequirement", Label = "Reserve Requirement", Category = category,
                    LatestText = latestRow?.ReserveRequirement, PreviousText = previousRow?.ReserveRequirement, LatestPeriod = latest.Periode
                });
                response.Cards.Add(new MetricCardDto
                {
                    Key = "trafficLight", Label = "Traffic Light", Category = category,
                    LatestText = latestRow?.TrafficLight, PreviousText = previousRow?.TrafficLight, LatestPeriod = latest.Periode
                });
            }

            return response;
        }

        // ---------------------------------------------------------------
        // Shared chart/card building + period-normalization helpers
        // ---------------------------------------------------------------

        private static List<(string Periode, string SortKey, T Summary)> OrderByPeriode<T>(List<(string Periode, T Summary)> items)
            => items
                .Select(i => (i.Periode, SortKey: NormalizePeriodeForSort(i.Periode), i.Summary))
                .OrderBy(i => i.SortKey, StringComparer.Ordinal)
                .ToList();

        private static LineChartDto BuildChart(ChartDef def, List<(string Periode, string SortKey, KajianRisikoSummaryDto Summary)> ordered)
        {
            // Status-only metrics (Selector == null) get a card but no chart series.
            var chartable = def.Metrics
                .Where(m => m.Selector != null)
                .Select(m => (m.Key, m.Label, Selector: m.Selector!))
                .ToList();

            var chart = BuildChart(def.Key, def.Title, def.Unit, chartable, ordered);
            chart.Group = def.Group;
            chart.GroupLabel = def.GroupLabel;
            return chart;
        }

        private static MetricCardDto BuildCard(
            string category, MetricDef metric, string? unit,
            List<(string Periode, string SortKey, KajianRisikoSummaryDto Summary)> ordered)
        {
            var card = new MetricCardDto { Key = metric.Key, Label = metric.Label, Category = category, Unit = unit };
            if (ordered.Count == 0) return card;

            var latest = ordered[^1];

            if (metric.Selector != null)
            {
                card.LatestValue = metric.Selector(latest.Summary);
                card.LatestPeriod = latest.Periode;

                if (ordered.Count >= 2)
                {
                    var previous = ordered[^2];
                    card.PreviousValue = metric.Selector(previous.Summary);
                    if (card.LatestValue.HasValue && card.PreviousValue.HasValue)
                    {
                        card.DeltaAbsolute = card.LatestValue - card.PreviousValue;
                        if (card.PreviousValue.Value != 0)
                            card.DeltaPercent = card.DeltaAbsolute / Math.Abs(card.PreviousValue.Value) * 100m;
                    }
                }
            }
            else if (metric.TextSelector != null)
            {
                card.LatestText = metric.TextSelector(latest.Summary);
                card.LatestPeriod = latest.Periode;

                if (ordered.Count >= 2)
                    card.PreviousText = metric.TextSelector(ordered[^2].Summary);
            }

            return card;
        }

        private static LineChartDto BuildChart<T>(
            string key, string title, string? unit,
            List<(string Key, string Label, Func<T, decimal?> Selector)> metrics,
            List<(string Periode, string SortKey, T Summary)> ordered)
        {
            var chart = new LineChartDto { Key = key, Title = title, Unit = unit };
            foreach (var (metricKey, label, selector) in metrics)
            {
                var series = new LineChartSeriesDto { Key = metricKey, Label = label };
                foreach (var (periode, sortKey, summary) in ordered)
                {
                    series.Points.Add(new LineChartPointDto
                    {
                        Period = periode,
                        SortKey = sortKey,
                        Value = selector(summary)
                    });
                }
                chart.Series.Add(series);
            }
            return chart;
        }

        private static MetricCardDto BuildCard<T>(
            string key, string label, string? unit,
            List<(string Periode, string SortKey, T Summary)> ordered,
            Func<T, decimal?> selector)
        {
            var card = new MetricCardDto { Key = key, Label = label, Unit = unit };
            if (ordered.Count == 0) return card;

            var latest = ordered[^1];
            card.LatestValue = selector(latest.Summary);
            card.LatestPeriod = latest.Periode;

            if (ordered.Count >= 2)
            {
                var previous = ordered[^2];
                card.PreviousValue = selector(previous.Summary);
                if (card.LatestValue.HasValue && card.PreviousValue.HasValue)
                {
                    card.DeltaAbsolute = card.LatestValue - card.PreviousValue;
                    if (card.PreviousValue.Value != 0)
                        card.DeltaPercent = card.DeltaAbsolute / Math.Abs(card.PreviousValue.Value) * 100m;
                }
            }

            return card;
        }

        private static LineChartSeriesDto BuildBranchSeries(
            string key, string label,
            List<(string Periode, string SortKey, ExtractResponseDto<ProfilMaturitasKlnDto> Result)> ordered,
            string branch, Func<ProfilMaturitasKlnDto, double> selector)
        {
            var series = new LineChartSeriesDto { Key = key, Label = label };
            foreach (var (periode, sortKey, result) in ordered)
            {
                var row = result.Data.FirstOrDefault(d => string.Equals(d.Cabang, branch, StringComparison.OrdinalIgnoreCase));
                series.Points.Add(new LineChartPointDto
                {
                    Period = periode,
                    SortKey = sortKey,
                    Value = row != null ? (decimal)selector(row) : null
                });
            }
            return series;
        }

        private static void AddBranchCard(
            DashboardResponseDto response, string category, string key, string label,
            List<(string Periode, string SortKey, ExtractResponseDto<ProfilMaturitasKlnDto> Result)> ordered,
            Func<ExtractResponseDto<ProfilMaturitasKlnDto>, decimal?> selector,
            string? unit = null)
        {
            if (ordered.Count == 0) return;

            var latest = ordered[^1];
            var card = new MetricCardDto { Key = key, Label = label, Category = category, Unit = unit };
            card.LatestValue = selector(latest.Result);
            card.LatestPeriod = latest.Periode;

            if (ordered.Count >= 2)
            {
                var previous = ordered[^2];
                card.PreviousValue = selector(previous.Result);
                if (card.LatestValue.HasValue && card.PreviousValue.HasValue)
                {
                    card.DeltaAbsolute = card.LatestValue - card.PreviousValue;
                    if (card.PreviousValue.Value != 0)
                        card.DeltaPercent = card.DeltaAbsolute / Math.Abs(card.PreviousValue.Value) * 100m;
                }
            }

            response.Cards.Add(card);
        }

        private static readonly Dictionary<string, int> IndonesianMonths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Januari"] = 1, ["Februari"] = 2, ["Maret"] = 3, ["April"] = 4, ["Mei"] = 5, ["Juni"] = 6,
            ["Juli"] = 7, ["Agustus"] = 8, ["September"] = 9, ["Oktober"] = 10, ["November"] = 11, ["Desember"] = 12
        };

        // Produces a chronologically-sortable key from whatever the sheet's period cell contained:
        // an ISO date ("2026-07-24"), an Indonesian "Month YYYY" label ("Juni 2026"), or (best
        // effort) the raw text itself if neither pattern matches.
        private static string NormalizePeriodeForSort(string? periode)
        {
            if (string.IsNullOrWhiteSpace(periode)) return "9999-99-99";
            var t = periode.Trim();

            if (DateTime.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
                return iso.ToString("yyyy-MM-dd");

            var m = Regex.Match(t, @"^([A-Za-z]+)\s+(\d{4})$");
            if (m.Success && IndonesianMonths.TryGetValue(m.Groups[1].Value, out var monthNum))
                return $"{m.Groups[2].Value}-{monthNum:D2}";

            return t;
        }

        private static string Slugify(string s)
            => Regex.Replace(s.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    }
}
