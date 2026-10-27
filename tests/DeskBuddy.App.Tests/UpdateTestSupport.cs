using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace DeskBuddy.App.Tests;

/// <summary>
/// What the tests of the update flow share: a stub in place of GitHub and the release it answers with.
/// </summary>
internal static class UpdateTestSupport
{
    /// <summary>
    /// What the stub serves as the installer.
    /// </summary>
    public static readonly byte[] InstallerBytes = Encoding.UTF8.GetBytes("pretend installer");

    /// <summary>
    /// The digest GitHub would report for <see cref="InstallerBytes"/>.
    /// </summary>
    public static readonly string InstallerDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(InstallerBytes));

    /// <summary>
    /// Builds the JSON GitHub answers with for the latest release.
    /// </summary>
    /// <param name="tag">
    /// The tag of the release.
    /// </param>
    /// <param name="assetName">
    /// The file name of the one asset.
    /// </param>
    /// <param name="digest">
    /// The digest GitHub reports for the asset, or <see langword="null"/> for none.
    /// </param>
    /// <returns>
    /// The JSON text.
    /// </returns>
    public static string ReleaseJson(string tag, string assetName, string? digest = null)
    {
        return $$"""
            {
              "id": 1,
              "tag_name": "{{tag}}",
              "name": "{{tag}}",
              "body": "Notes",
              "draft": false,
              "prerelease": false,
              "html_url": "https://github.com/coldhighsun/DeskBuddy/releases/tag/{{tag}}",
              "published_at": "2026-01-01T00:00:00Z",
              "created_at": "2026-01-01T00:00:00Z",
              "assets": [
                {
                  "id": 2,
                  "name": "{{assetName}}",
                  "size": {{InstallerBytes.Length}},
                  "digest": {{(digest is null ? "null" : '"' + digest + '"')}},
                  "content_type": "application/octet-stream",
                  "url": "https://api.github.com/repos/coldhighsun/DeskBuddy/releases/assets/2",
                  "browser_download_url": "https://github.com/coldhighsun/DeskBuddy/releases/download/{{tag}}/{{assetName}}",
                  "created_at": "2026-01-01T00:00:00Z"
                }
              ]
            }
            """;
    }

    /// <summary>
    /// Answers every request with the same canned response and counts them.
    /// </summary>
    public sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>
        /// The status of the answer.
        /// </summary>
        private HttpStatusCode _status = HttpStatusCode.OK;

        /// <summary>
        /// The body of the answer.
        /// </summary>
        private string _body = "{}";

        /// <summary>
        /// Gets how many requests were answered.
        /// </summary>
        public int RequestCount { get; private set; }

        /// <summary>
        /// Gets the path of the last request.
        /// </summary>
        public string LastRequestPath { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the answer given from now on.
        /// </summary>
        /// <param name="status">
        /// The status of the answer.
        /// </param>
        /// <param name="body">
        /// The JSON body of the answer.
        /// </param>
        public void Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestPath = request.RequestUri!.AbsolutePath;
            var isAsset = request.RequestUri.AbsolutePath.Contains("/releases/assets/", StringComparison.Ordinal);
            var response = new HttpResponseMessage(_status)
            {
                Content = isAsset
                    ? new ByteArrayContent(InstallerBytes)
                    : new StringContent(_body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
            return Task.FromResult(response);
        }
    }
}
