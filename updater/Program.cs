using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;

namespace StickMayhemUpdater {
    internal static class Program {
        static readonly string[] Installable = { "StickMayhem.exe", "README.md", "使用说明.txt", "CHANGELOG.md" };
        [STAThread] static int Main(string[] args) {
            try {
                if (args.Length != 6 || args[0] != "--install") throw new InvalidOperationException("更新器参数无效。");
                int parentId; if (!Int32.TryParse(args[1], out parentId)) throw new InvalidOperationException("找不到正在更新的游戏进程。");
                string root = Path.GetFullPath(args[2]).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string ownRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!String.Equals(root, ownRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新目录与更新器所在目录不一致。");
                string package = Path.GetFullPath(args[3]); string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!package.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(package)) throw new InvalidOperationException("更新包不在临时目录中或文件不存在。");
                string expected = args[4]; if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) throw new InvalidOperationException("更新包校验值无效。");
                VerifyHash(package, expected);
                WaitForGame(parentId);

                string work = Path.Combine(tempRoot, "StickMayhem-install-" + Guid.NewGuid().ToString("N"));
                string stage = Path.Combine(work, "stage"), backup = Path.Combine(work, "backup");
                Directory.CreateDirectory(stage); Directory.CreateDirectory(backup);
                try {
                    ExtractAllowed(package, stage);
                    if (!File.Exists(Path.Combine(stage, "StickMayhem.exe"))) throw new InvalidDataException("更新包中缺少游戏主程序。");
                    var changed = new List<string>();
                    try {
                        foreach (string name in Installable) {
                            string source = Path.Combine(stage, name); if (!File.Exists(source)) continue;
                            string target = Path.Combine(root, name);
                            if (File.Exists(target)) File.Copy(target, Path.Combine(backup, name), true);
                            File.Copy(source, target, true); changed.Add(name);
                        }
                        var info = Process.Start(new ProcessStartInfo { FileName = Path.Combine(root, "StickMayhem.exe"), WorkingDirectory = root, UseShellExecute = true });
                        if (info == null) throw new IOException("新版启动失败。");
                        if (info.WaitForExit(2500) && info.ExitCode != 0) throw new IOException("新版启动失败，已恢复旧版本。");
                    } catch {
                        for (int i = changed.Count - 1; i >= 0; i--) {
                            string name = changed[i], target = Path.Combine(root, name), saved = Path.Combine(backup, name);
                            try { if (File.Exists(saved)) File.Copy(saved, target, true); else if (File.Exists(target)) File.Delete(target); } catch { }
                        }
                        throw;
                    }
                    MessageBox.Show("已更新至 v" + args[5] + "。", "Chaos gun fight", MessageBoxButton.OK, MessageBoxImage.Information);
                } finally { try { Directory.Delete(work, true); } catch { } }
                try { File.Delete(package); } catch { }
                return 0;
            } catch (Exception ex) {
                MessageBox.Show("安装更新失败，原版本已尽量恢复。\n\n" + ex.Message, "Chaos gun fight 更新", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
        static void WaitForGame(int pid) {
            try { using (var process = Process.GetProcessById(pid)) if (!process.WaitForExit(45000)) throw new TimeoutException("等待游戏退出超时，请关闭游戏后重试。"); }
            catch (ArgumentException) { }
        }
        static void VerifyHash(string package, string expected) {
            string actual; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(package))
                actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 校验失败，未安装更新。");
        }
        static void ExtractAllowed(string package, string stage) {
            using (var archive = ZipFile.OpenRead(package)) {
                foreach (var entry in archive.Entries) {
                    string name = entry.FullName;
                    if (name.Length == 0 || name.Contains("/") || name.Contains("\\")) throw new InvalidDataException("更新包包含不允许的路径。");
                    if (name == "StickMayhem.Update.exe") continue;
                    if (!Installable.Contains(name)) throw new InvalidDataException("更新包包含未知文件：" + name);
                    if (entry.Length > 50L * 1024 * 1024) throw new InvalidDataException("更新文件超过大小限制。");
                    using (var input = entry.Open()) using (var output = new FileStream(Path.Combine(stage, name), FileMode.CreateNew, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                }
            }
        }
    }
}
