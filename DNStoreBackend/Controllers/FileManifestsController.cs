using DNStoreBackend.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DNStoreBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileManifestsController : ControllerBase
    {
        private readonly string manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "state", "filemanifests.json");
        private static readonly SemaphoreSlim FileLock = new(1, 1);

        public FileManifestsController()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        }

        [HttpGet("{ownerAddress}")]
        public async Task<IActionResult> GetForOwner(string ownerAddress)
        {
            var manifests = await LoadManifests();
            return Ok(manifests
                .Where(m => m.OwnerAddress.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.UploadTime)
                .ToList());
        }

        [HttpPost]
        public async Task<IActionResult> Upsert(FileManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.OwnerAddress) ||
                string.IsNullOrWhiteSpace(manifest.FileName) ||
                string.IsNullOrWhiteSpace(manifest.FileHash) ||
                manifest.ShardHashes.Count == 0)
            {
                return BadRequest("Manifest is incomplete.");
            }

            var manifests = await LoadManifests();
            manifests.RemoveAll(m =>
                m.OwnerAddress.Equals(manifest.OwnerAddress, StringComparison.OrdinalIgnoreCase) &&
                m.FileHash.Equals(manifest.FileHash, StringComparison.OrdinalIgnoreCase));

            manifests.Add(manifest);
            await SaveManifests(manifests);
            return Ok();
        }

        [HttpDelete("{ownerAddress}/{fileHash}")]
        public async Task<IActionResult> Delete(string ownerAddress, string fileHash)
        {
            var manifests = await LoadManifests();
            int removed = manifests.RemoveAll(m =>
                m.OwnerAddress.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase) &&
                m.FileHash.Equals(fileHash, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                await SaveManifests(manifests);
            }

            return Ok();
        }

        private async Task<List<FileManifest>> LoadManifests()
        {
            await FileLock.WaitAsync();
            try
            {
                if (!System.IO.File.Exists(manifestPath))
                    return new List<FileManifest>();

                string json = await System.IO.File.ReadAllTextAsync(manifestPath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<FileManifest>();

                return JsonSerializer.Deserialize<List<FileManifest>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<FileManifest>();
            }
            finally
            {
                FileLock.Release();
            }
        }

        private async Task SaveManifests(List<FileManifest> manifests)
        {
            await FileLock.WaitAsync();
            try
            {
                string json = JsonSerializer.Serialize(manifests, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(manifestPath, json);
            }
            finally
            {
                FileLock.Release();
            }
        }
    }
}
