using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AttachmentsController : ControllerBase
    {
        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB
        private static readonly string[] AllowedContentTypes =
        [
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf", "video/mp4"
        ];

        // Nullable/optional — same graceful-degradation pattern as
        // UsersController's avatar endpoints: a missing/misconfigured Blob
        // connection string shouldn't take down DI for this whole controller
        // (was previously a hard dependency, so every upload attempt threw
        // an unhandled 500 instead of a clean 503 when Blob wasn't configured).
        private readonly BlobServiceClient? _blobServiceClient;

        public AttachmentsController(BlobServiceClient? blobServiceClient = null)
        {
            _blobServiceClient = blobServiceClient;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is empty.");

            if (file.Length > MaxFileSizeBytes)
                return BadRequest("File exceeds the 20 MB size limit.");

            if (!AllowedContentTypes.Contains(file.ContentType))
                return BadRequest("File type not allowed.");

            if (_blobServiceClient == null)
                return StatusCode(503, "File uploads are not configured.");

            var container = _blobServiceClient.GetBlobContainerClient("attachments");
            // Blob-level anonymous read — the response returns a plain URL
            // (no SAS token), which only resolves in the browser (<img>,
            // download links) if the blob itself is publicly readable.
            // PublicAccessType.None made every upload a private blob nobody
            // could actually view. Program.cs also fixes this up at startup
            // for containers that already existed with the wrong setting.
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobName = $"{System.Guid.NewGuid()}/{file.FileName}";
            var blob = container.GetBlobClient(blobName);
            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

            return Ok(new { url = blob.Uri.ToString() });
        }
    }
}
