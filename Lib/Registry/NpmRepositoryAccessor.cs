using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Lib.Registry
{
    public class NpmRepositoryAccessor : IDisposable
    {
        HttpClient _httpClient;

        public NpmRepositoryAccessor(string repoUrl = "https://registry.npmjs.org/")
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(repoUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<(EntityTagHeaderValue etag, string content)> GetPackageInfo(string name,
            EntityTagHeaderValue etag)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, name);
            if (etag != null)
                req.Headers.IfNoneMatch.Add(etag);
            var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return (etag, null);
            }

            return (response.Headers.ETag, await response.Content.ReadAsStringAsync());
        }

        public async Task<byte[]> GetPackageTgz(string packageName, string tgzName)
        {
            var response =
                await _httpClient.GetAsync($"{packageName}/-/{tgzName}", HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                throw new IOException(
                    $"Getting {_httpClient.BaseAddress}/{packageName}/-/{tgzName} failed with {response.StatusCode} {response.ReasonPhrase}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
