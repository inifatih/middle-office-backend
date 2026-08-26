using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace middle_office_backend.Rmis.Api.Controllers.MiddleOffice
{
    [Route("api/middle-office")]
    [ApiController]
    public class MiddleOfficeController : ControllerBase
    {
        private readonly IMiddleOfficeServices _service;
        private readonly IWebHostEnvironment _env;

        public MiddleOfficeController(IMiddleOfficeServices service, IWebHostEnvironment env)
        {
            _service = service;
            _env = env;
        }

        [HttpPost("extract/kajian-risiko")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ExtractKajianRisiko([FromForm] IFormFile file, CancellationToken ct)
        {
            var (success, result) = await _service.ExtractKajianRisikoAsync(file, ct);
            if (!success) return BadRequest(new { isSuccess = false, message = result.Message });
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpPost("extract/profil-kln")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ExtractProfilMaturitasKln([FromForm] IFormFile file, CancellationToken ct)
        {
            var (success, result) = await _service.ExtractProfilMaturitasKlnAsync(file, ct);
            if (!success) return BadRequest(new { isSuccess = false, message = result.Message });
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpPost("extract/resume-ho")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ExtractResumeMatProfHo([FromForm] IFormFile file, CancellationToken ct)
        {
            var (success, result) = await _service.ExtractResumeMatProfHoAsync(file, ct);
            if (!success) return BadRequest(new { isSuccess = false, message = result.Message });
            return Ok(new { isSuccess = true, data = result });
        }

        // =====================================================================
        // Bundled reference-file endpoints: until real upload/approve persistence
        // exists, dashboards read this — same extraction service, real numbers,
        // instead of hardcoded frontend mock data.
        // =====================================================================

        [HttpGet("kajian-risiko/sample")]
        public async Task<IActionResult> GetKajianRisikoSample(CancellationToken ct)
        {
            var file = LoadSampleFile("kajian-risiko.xlsx");
            if (file == null) return NotFound(new { isSuccess = false, message = "Sample data belum tersedia." });

            var (success, result) = await _service.ExtractKajianRisikoAsync(file, ct);
            if (!success) return BadRequest(new { isSuccess = false, message = result.Message });
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpGet("profil-kln/sample")]
        public async Task<IActionResult> GetProfilKlnSample(CancellationToken ct)
        {
            var file = LoadSampleFile("profil-kln.xlsx");
            if (file == null) return NotFound(new { isSuccess = false, message = "Sample data belum tersedia." });

            var (success, result) = await _service.ExtractProfilMaturitasKlnAsync(file, ct);
            if (!success) return BadRequest(new { isSuccess = false, message = result.Message });
            return Ok(new { isSuccess = true, data = result });
        }

        [HttpGet("resume-ho/sample")]
        public async Task<IActionResult> GetResumeHoSample(CancellationToken ct)
        {
            var file = LoadSampleFile("resume-ho.xlsx");
            if (file == null) return NotFound(new { isSuccess = false, message = "Sample data belum tersedia." });

            var (success, result) = await _service.ExtractResumeMatProfHoAsync(file, ct);
            if (!success) return BadRequest(new { isSuccess = false, message = result.Message });
            return Ok(new { isSuccess = true, data = result });
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
