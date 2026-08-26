using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace middle_office_backend.Rmis.Api.Controllers.MiddleOffice
{
    // Stateless dashboard endpoints: the caller uploads one workbook per period they want plotted
    // (e.g. one "Kajian Risiko" file per day) and gets back chart-ready time series + metric cards
    // in one response. No persistence yet — see IDashboardService for the plan once a DB lands.
    [Route("api/middle-office/dashboard")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboard;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        // Bundled sample files never change at runtime, so the resulting DashboardResponseDto
        // (small JSON — a handful of periods' cards/charts) is cached indefinitely per report
        // instead of re-parsing 3 xlsx files on every dashboard view/filter change.
        private static readonly MemoryCacheEntryOptions SampleCacheOptions = new() { Priority = CacheItemPriority.NeverRemove };

        public DashboardController(IDashboardService dashboard, IWebHostEnvironment env, IMemoryCache cache)
        {
            _dashboard = dashboard;
            _env = env;
            _cache = cache;
        }

        [HttpPost("kajian-risiko")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> KajianRisiko([FromForm] List<IFormFile> files, CancellationToken ct)
        {
            if (files == null || files.Count == 0) return BadRequest(new { isSuccess = false, message = "Minimal satu file diperlukan (satu file per periode)." });
            var result = await _dashboard.BuildKajianRisikoDashboardAsync(files, ct);
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpPost("resume-ho/{tipe}")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ResumeHo(string tipe, [FromForm] List<IFormFile> files, CancellationToken ct)
        {
            var sheetName = NormalizeResumeHoTipe(tipe);
            if (sheetName == null) return BadRequest(new { isSuccess = false, message = "tipe harus 'kontraktual' atau 'behavioral'." });
            if (files == null || files.Count == 0) return BadRequest(new { isSuccess = false, message = "Minimal satu file diperlukan (satu file per periode)." });

            var result = await _dashboard.BuildResumeHoDashboardAsync(files, sheetName, ct);
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpPost("profil-kln")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ProfilKln([FromForm] List<IFormFile> files, CancellationToken ct)
        {
            if (files == null || files.Count == 0) return BadRequest(new { isSuccess = false, message = "Minimal satu file diperlukan (satu file per periode)." });
            var result = await _dashboard.BuildProfilKlnDashboardAsync(files, ct);
            return Ok(new { isSuccess = true, data = result });
        }

        // =====================================================================
        // Sample-data previews: single-file, single-period (a chart with 1 point).
        // Wire up the frontend against these before real multi-period uploads exist.
        // =====================================================================

        // Bundled periods so the sample preview is a genuine time series instead of a single point.
        // Kajian Risiko is a daily report, so its sample is a dense run of consecutive days (enough
        // to fill more than one 5-day range window); Profil KLN is monthly, so its sample is a run
        // of consecutive months (enough to fill more than one 5-month range window). Real data: 24
        // Jul 2026 (Kajian Risiko) / Jun 2026 (Profil KLN); every other period is generated from that
        // real file with a small proportional variation trending toward the real value.
        private static readonly string[] KajianRisikoSampleFiles =
        {
            "kajian-risiko-2026-07-15.xlsx", "kajian-risiko-2026-07-16.xlsx", "kajian-risiko-2026-07-17.xlsx",
            "kajian-risiko-2026-07-18.xlsx", "kajian-risiko-2026-07-19.xlsx", "kajian-risiko-2026-07-20.xlsx",
            "kajian-risiko-2026-07-21.xlsx", "kajian-risiko-2026-07-22.xlsx", "kajian-risiko-2026-07-23.xlsx",
            "kajian-risiko-2026-07-24.xlsx"
        };
        private static readonly string[] ProfilKlnSampleFiles =
        {
            "profil-kln-2026-02.xlsx", "profil-kln-2026-03.xlsx", "profil-kln-2026-04.xlsx", "profil-kln-2026-05.xlsx",
            "profil-kln-2026-06.xlsx", "profil-kln-2026-07.xlsx", "profil-kln-2026-08.xlsx"
        };

        [HttpGet("kajian-risiko/sample")]
        public async Task<IActionResult> KajianRisikoSample(CancellationToken ct)
        {
            var result = await _cache.GetOrCreateAsync("dashboard-sample:kajian-risiko", async entry =>
            {
                entry.SetOptions(SampleCacheOptions);
                var files = LoadSampleFiles(KajianRisikoSampleFiles);
                if (files.Count == 0) return null;
                return await _dashboard.BuildKajianRisikoDashboardAsync(files, ct);
            });

            if (result == null) return NotFound(new { isSuccess = false, message = "Sample data belum tersedia." });
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpGet("resume-ho/{tipe}/sample")]
        public async Task<IActionResult> ResumeHoSample(string tipe, CancellationToken ct)
        {
            var sheetName = NormalizeResumeHoTipe(tipe);
            if (sheetName == null) return BadRequest(new { isSuccess = false, message = "tipe harus 'kontraktual' atau 'behavioral'." });

            var file = LoadSampleFile("resume-ho.xlsx");
            if (file == null) return NotFound(new { isSuccess = false, message = "Sample data belum tersedia." });
            var result = await _dashboard.BuildResumeHoDashboardAsync(new[] { file }, sheetName, ct);
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpGet("profil-kln/sample")]
        public async Task<IActionResult> ProfilKlnSample(CancellationToken ct)
        {
            var result = await _cache.GetOrCreateAsync("dashboard-sample:profil-kln", async entry =>
            {
                entry.SetOptions(SampleCacheOptions);
                var files = LoadSampleFiles(ProfilKlnSampleFiles);
                if (files.Count == 0) return null;
                return await _dashboard.BuildProfilKlnDashboardAsync(files, ct);
            });

            if (result == null) return NotFound(new { isSuccess = false, message = "Sample data belum tersedia." });
            return Ok(new { isSuccess = true, data = result });
        }

        private static string? NormalizeResumeHoTipe(string tipe) => tipe?.Trim().ToLowerInvariant() switch
        {
            "kontraktual" or "contractual" => "Kontraktual",
            "behavioral" => "Behavioral",
            _ => null
        };

        private List<IFormFile> LoadSampleFiles(IEnumerable<string> fileNames)
        {
            var files = new List<IFormFile>();
            foreach (var fileName in fileNames)
            {
                var file = LoadSampleFile(fileName);
                if (file != null) files.Add(file);
            }
            return files;
        }

        private IFormFile? LoadSampleFile(string fileName)
        {
            var path = Path.Combine(_env.ContentRootPath, "SampleData", fileName);
            if (!System.IO.File.Exists(path)) return null;

            var bytes = System.IO.File.ReadAllBytes(path);
            var stream = new MemoryStream(bytes);

            return new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }
    }
}
