using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace StickMayhem {
    public sealed class UpdateManifest {
        public string Version { get; set; }
        public string PackageUrl { get; set; }
        public string Sha256 { get; set; }
        public string Notes { get; set; }
    }

    public static class UpdateService {
        public const string ManifestUrl = "https://raw.githubusercontent.com/EM-py/Chaos-gun-fight/main/update.json";
        const string PackageUrl = "https://raw.githubusercontent.com/EM-py/Chaos-gun-fight/main/StickMayhem-share.zip";
        const string CdnManifestUrl = "https://cdn.jsdelivr.net/gh/EM-py/Chaos-gun-fight@main/update.json";
        const string CdnPackageUrl = "https://cdn.jsdelivr.net/gh/EM-py/Chaos-gun-fight@main/StickMayhem-share.zip";

        static void ConfigureTls() {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        }
        static HttpWebRequest Request(string url) {
            ConfigureTls();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET"; request.UserAgent = "StickMayhem-Updater/1";
            request.Accept = "application/json, application/octet-stream, */*";
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            request.Timeout = 20000; request.ReadWriteTimeout = 20000;
            Uri uri = new Uri(url);
            if (String.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)) {
                string authorization = GetGitHubAuthorization();
                if (!String.IsNullOrEmpty(authorization)) request.Headers[HttpRequestHeader.Authorization] = authorization;
            }
            return request;
        }
        // Reuse a GitHub credential already stored by Git Credential Manager (for example via GitHub Desktop).
        // Never persist the token in the game directory or include it in diagnostics.
        static string GetGitHubAuthorization() {
            Process process = null;
            try {
                var start = new ProcessStartInfo("git", "credential fill") {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                start.EnvironmentVariables["GCM_INTERACTIVE"] = "Never";
                process = Process.Start(start);
                if (process == null) return null;
                process.BeginErrorReadLine();
                process.StandardInput.Write("protocol=https\nhost=github.com\npath=EM-py/Chaos-gun-fight.git\n\n");
                process.StandardInput.Close();
                if (!process.WaitForExit(8000)) { try { process.Kill(); } catch { } return null; }
                string username = null, password = null;
                string output = process.StandardOutput.ReadToEnd();
                foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                    int separator = line.IndexOf('='); if (separator <= 0) continue;
                    string key = line.Substring(0, separator), value = line.Substring(separator + 1);
                    if (key == "username") username = value;
                    else if (key == "password") password = value;
                }
                if (String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password)) return null;
                return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            } catch { return null; }
            finally { if (process != null) process.Dispose(); }
        }
        public static UpdateManifest CheckLatest() {
            try { return ReadManifest(ManifestUrl); }
            catch (WebException) { return ReadManifest(CdnManifestUrl); }
        }
        static UpdateManifest ReadManifest(string url) {
            using (var response = (HttpWebResponse)Request(url).GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true)) {
                string json = reader.ReadToEnd();
                if (json.Length > 65536) throw new InvalidDataException("更新信息超出大小限制。");
                return ParseManifest(json);
            }
        }
        public static string FriendlyError(Exception error) {
            var web = error as WebException;
            var response = web == null ? null : web.Response as HttpWebResponse;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                return "GitHub 返回 404：更新仓库/文件不存在，或仓库尚未设为公开。分享给其他人自动更新时，仓库必须为 Public。";
            return error == null ? "无法连接更新服务器。" : error.Message;
        }
        internal static UpdateManifest ParseManifest(string json) {
            var manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(json);
            Version parsed;
            Uri package;
            if (manifest == null || !System.Version.TryParse(manifest.Version, out parsed) ||
                !Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out package) || package.Scheme != Uri.UriSchemeHttps ||
                !(String.Equals(package.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) && String.Equals(package.AbsolutePath, "/EM-py/Chaos-gun-fight/main/StickMayhem-share.zip", StringComparison.Ordinal) ||
                  String.Equals(package.Host, "cdn.jsdelivr.net", StringComparison.OrdinalIgnoreCase) && String.Equals(package.AbsolutePath, "/gh/EM-py/Chaos-gun-fight@main/StickMayhem-share.zip", StringComparison.Ordinal)) ||
                String.IsNullOrWhiteSpace(manifest.Sha256) || manifest.Sha256.Length != 64 ||
                !manifest.Sha256.All(Uri.IsHexDigit) || String.IsNullOrWhiteSpace(manifest.Notes))
                throw new InvalidDataException("GitHub 更新信息格式无效或下载地址不受信任。");
            manifest.Version = parsed.ToString(3);
            manifest.Sha256 = manifest.Sha256.ToLowerInvariant();
            return manifest;
        }
        public static string CurrentVersion {
            get { int slash = Rules.Version.LastIndexOf('/'); return slash < 0 ? Rules.Version : Rules.Version.Substring(slash + 1); }
        }
        public static bool IsNewer(string candidate, string current) {
            Version next, old;
            return Version.TryParse(candidate, out next) && Version.TryParse(current, out old) && next > old;
        }
        public static string DownloadVerified(UpdateManifest manifest) {
            string folder = Path.Combine(Path.GetTempPath(), "StickMayhemUpdates");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
            try {
                using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    try { DownloadTo(manifest.PackageUrl, output); }
                    catch (WebException) { output.SetLength(0); DownloadTo(CdnPackageUrl + "?version=" + Uri.EscapeDataString(manifest.Version), output); }
                }
                string hash;
                using (var sha = SHA256.Create()) using (var input = File.OpenRead(file))
                    hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (!String.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256 校验失败，安装已阻止。");
                return file;
            } catch { try { if (File.Exists(file)) File.Delete(file); } catch { } throw; }
        }
        static void DownloadTo(string url, Stream output) {
            using (var response = (HttpWebResponse)Request(url).GetResponse())
            using (var input = response.GetResponseStream()) {
                byte[] buffer = new byte[65536]; int count; long total = 0;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                    total += count; if (total > 100L * 1024 * 1024) throw new InvalidDataException("更新包超过 100 MB 限制。");
                    output.Write(buffer, 0, count);
                }
            }
        }
    }
}
