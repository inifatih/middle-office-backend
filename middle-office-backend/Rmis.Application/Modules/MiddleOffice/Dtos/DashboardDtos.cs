namespace middle_office_backend.Rmis.Application.Modules.MiddleOffice.Dtos
{
    // Generic chart/card shapes shared by all three dashboards. Stateless for now (PRD calls for
    // persisting to a DB + file storage later) — a "period" here is simply whichever period each
    // uploaded file represents, not a stored history. The caller uploads one file per period they
    // want plotted (e.g. one file per day/month) and gets back a ready-to-render time series.

    public class LineChartPointDto
    {
        public string Period { get; set; } = string.Empty; // display label, e.g. "24 Jul 2026" or "Juni 2026"
        public string SortKey { get; set; } = string.Empty; // normalized, chronologically sortable
        public decimal? Value { get; set; }
    }

    public class LineChartSeriesDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<LineChartPointDto> Points { get; set; } = new();
    }

    public class LineChartDto
    {
        public string Key { get; set; } = string.Empty; // sub-category key, e.g. "kasRupiah"
        public string Title { get; set; } = string.Empty; // sub-category label, e.g. "Kas Rupiah"
        public string Group { get; set; } = string.Empty; // letter-group key, e.g. "A"
        public string GroupLabel { get; set; } = string.Empty; // e.g. "A. Primary Reserve Rupiah"
        public string? Unit { get; set; }
        public List<LineChartSeriesDto> Series { get; set; } = new();
    }

    public class MetricCardDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // matching LineChartDto.Key this card belongs under
        public string? Unit { get; set; }
        public decimal? LatestValue { get; set; }
        public string? LatestText { get; set; } // for status/text-only metrics (Kondisi, Traffic Light, Posisi, ...)
        public string? LatestPeriod { get; set; }
        public decimal? PreviousValue { get; set; }
        public string? PreviousText { get; set; }
        public decimal? DeltaAbsolute { get; set; }
        public decimal? DeltaPercent { get; set; }
    }

    public class DashboardResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public int PeriodsRequested { get; set; }
        public int PeriodsProcessed { get; set; }
        public List<string> Warnings { get; set; } = new(); // per-file extraction failures, kept non-fatal
        public List<MetricCardDto> Cards { get; set; } = new();
        public List<LineChartDto> Charts { get; set; } = new();
    }
}
