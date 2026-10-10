using System;
using System.IO;
using System.Linq;
using System.Net;
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
            return request;
        }
        public static UpdateManifest CheckLatest() {
            using (var response = (HttpWebResponse)Request(ManifestUrl).GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true)) {
                string json = reader.ReadToEnd();
                if (json.Length > 65536) throw new InvalidDataException("更新信息超出大小限制。");
                return ParseManifest(json);
            }
        }
        internal static UpdateManifest ParseManifest(string json) {
            var manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(json);
            Version parsed;
            Uri package;
            if (manifest == null || !System.Version.TryParse(manifest.Version, out parsed) ||
                !Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out package) || package.Scheme != Uri.UriSchemeHttps ||
                !String.Equals(package.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(package.AbsolutePath, "/EM-py/Chaos-gun-fight/main/StickMayhem-share.zip", StringComparison.Ordinal) ||
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
                using (var response = (HttpWebResponse)Request(manifest.PackageUrl).GetResponse())
                using (var input = response.GetResponseStream())
                using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    byte[] buffer = new byte[65536]; int count; long total = 0;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                        total += count; if (total > 100L * 1024 * 1024) throw new InvalidDataException("更新包超过 100 MB 限制。");
                        output.Write(buffer, 0, count);
                    }
                }
                string hash;
                using (var sha = SHA256.Create()) using (var input = File.OpenRead(file))
                    hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (!String.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256 校验失败，安装已阻止。");
                return file;
            } catch { try { if (File.Exists(file)) File.Delete(file); } catch { } throw; }
        }
    }
}
