using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DeezFiles.Services
{
    public class NetworkService
    {
        private static string baseURL = "https://blockchain-g5aaa6h9dzdkaycb.centralindia-01.azurewebsites.net/api/";
        public static async Task<HttpResponseMessage> SendGetRequest(string URL)
        {
            string requestURL = baseURL + URL;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(requestURL);
            return response;
        }

        public static async Task<HttpResponseMessage> SendPostRequest(string URL, HttpContent data)
        {
            string requestURL = baseURL + URL;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.PostAsync(requestURL, data);
            return response;
        }

        public static async Task<HttpResponseMessage> SendDeleteRequest(string URL)
        {
            string requestURL = baseURL + URL;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.DeleteAsync(requestURL);
            return response;
        }

        public static async Task<bool> UploadShardToRelay(string shardHash, byte[] shardData)
        {
            try
            {
                string requestURL = baseURL + $"Shards/{shardHash}";
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                var content = new ByteArrayContent(shardData);
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                HttpResponseMessage response = await client.PostAsync(requestURL, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkService] Relay upload failed: {ex.Message}");
                return false;
            }
        }

        public static async Task<byte[]> DownloadShardFromRelay(string shardHash)
        {
            try
            {
                string requestURL = baseURL + $"Shards/{shardHash}";
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                HttpResponseMessage response = await client.GetAsync(requestURL);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync();
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkService] Relay download failed: {ex.Message}");
                return null;
            }
        }

    }
}
