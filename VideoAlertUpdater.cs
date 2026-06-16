using System;
using System.IO;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;

namespace VideoAlertUpdater
{
    // ════════════════════════════════════════════════════════════════════
    //  VERSION INFO — редагуй тільки тут
    // ════════════════════════════════════════════════════════════════════
    public static class UpdaterInfo
    {
        public const string DLL_VERSION = "1.0.0";
    }

    // ════════════════════════════════════════════════════════════════════
    //  RESULT — повертається у Streamer.bot C# код
    // ════════════════════════════════════════════════════════════════════
    public class UpdateResult
    {
        public bool  Success      { get; set; }
        public bool  Updated      { get; set; }   // true = були скачані нові файли
        public string Message     { get; set; }
        public string Version     { get; set; }
        public List<string> UpdatedFiles { get; set; } = new List<string>();
        public List<string> Errors       { get; set; } = new List<string>();
    }

    // ════════════════════════════════════════════════════════════════════
    //  MAIN UPDATER CLASS
    // ════════════════════════════════════════════════════════════════════
    public class Updater
    {
        // ── Публічне API ─────────────────────────────────────────────────
        /// <summary>
        /// Перевіряє GitHub і оновлює файли якщо є нова версія.
        /// </summary>
        /// <param name="rawBaseUrl">
        ///   URL до папки на raw.githubusercontent.com
        ///   Приклад: "https://raw.githubusercontent.com/YOUR_USER/YOUR_REPO/main/reward"
        /// </param>
        /// <param name="localFolder">
        ///   Локальна папка де лежать index.html і overlay.html
        ///   Приклад: @"D:\Program\obs\Obs Twich Reward\reward"
        /// </param>
        /// <param name="logPath">
        ///   Шлях до лог-файлу (може бути null — тоді лог не пишеться)
        /// </param>
        public static UpdateResult CheckAndUpdate(
            string rawBaseUrl,
            string localFolder,
            string logPath = null)
        {
            var result = new UpdateResult();

            try
            {
                Log(logPath, "INFO", $"=== VideoAlertUpdater v{UpdaterInfo.DLL_VERSION} START ===");
                Log(logPath, "INFO", $"Source : {rawBaseUrl}");
                Log(logPath, "INFO", $"Target : {localFolder}");

                // ── 1. Перевіряємо чи папка існує ────────────────────────
                if (!Directory.Exists(localFolder))
                {
                    try { Directory.CreateDirectory(localFolder); }
                    catch (Exception ex)
                    {
                        return Fail(result, $"Не вдалось створити папку: {ex.Message}", logPath);
                    }
                }

                // ── 2. Завантажуємо version.json з GitHub ─────────────────
                string versionUrl = rawBaseUrl.TrimEnd('/') + "/version.json";
                string versionJson;
                try
                {
                    versionJson = Download(versionUrl);
                    Log(logPath, "INFO", $"version.json: {versionJson.Trim()}");
                }
                catch (Exception ex)
                {
                    return Fail(result, $"Не вдалось завантажити version.json: {ex.Message}", logPath);
                }

                // ── 3. Парсимо version.json ────────────────────────────────
                string remoteVersion = ParseJsonField(versionJson, "version");
                string remoteNotes   = ParseJsonField(versionJson, "notes")   ?? "";

                if (string.IsNullOrEmpty(remoteVersion))
                {
                    return Fail(result, "version.json не містить поля 'version'", logPath);
                }

                result.Version = remoteVersion;

                // ── 4. Читаємо локальну версію ────────────────────────────
                string localVersionFile = Path.Combine(localFolder, "version.json");
                string localVersion     = "0.0.0";
                if (File.Exists(localVersionFile))
                {
                    try
                    {
                        string lv = File.ReadAllText(localVersionFile, Encoding.UTF8);
                        localVersion = ParseJsonField(lv, "version") ?? "0.0.0";
                    }
                    catch { localVersion = "0.0.0"; }
                }

                Log(logPath, "INFO", $"Версія локальна={localVersion} | remote={remoteVersion}");

                // ── 5. Визначаємо які файли потребують оновлення ──────────
                //      Використовуємо ОБИДВА методи: версія + SHA-256 хеш
                string[] targets = { "index.html", "overlay.html" };
                var filesToUpdate = new List<string>();

                bool versionNewer = CompareVersions(remoteVersion, localVersion) > 0;

                // Завантажуємо хеші з version.json (необов'язково)
                // Формат: "hashes": { "index.html": "abc123...", "overlay.html": "def456..." }
                var remoteHashes = ParseHashesBlock(versionJson);

                foreach (string fileName in targets)
                {
                    string localFile = Path.Combine(localFolder, fileName);
                    bool needUpdate  = false;

                    if (versionNewer)
                    {
                        needUpdate = true;
                        Log(logPath, "INFO", $"{fileName}: нова версія ({localVersion}→{remoteVersion})");
                    }
                    else if (!File.Exists(localFile))
                    {
                        needUpdate = true;
                        Log(logPath, "INFO", $"{fileName}: файл відсутній");
                    }
                    else if (remoteHashes.ContainsKey(fileName))
                    {
                        // Навіть якщо версія та сама — перевіряємо хеш
                        string localHash  = Sha256File(localFile);
                        string remoteHash = remoteHashes[fileName].ToLowerInvariant();
                        if (localHash != remoteHash)
                        {
                            needUpdate = true;
                            Log(logPath, "WARN", $"{fileName}: хеш різниться local={localHash.Substring(0,8)}… remote={remoteHash.Substring(0,8)}…");
                        }
                    }

                    if (needUpdate) filesToUpdate.Add(fileName);
                }

                // ── 6. Якщо нічого не змінилось ──────────────────────────
                if (filesToUpdate.Count == 0)
                {
                    Log(logPath, "INFO", "✓ Файли актуальні, оновлення не потрібне");
                    result.Success = true;
                    result.Updated = false;
                    result.Message = $"Файли актуальні (v{remoteVersion})";
                    return result;
                }

                // ── 7. Скачуємо і замінюємо файли ────────────────────────
                foreach (string fileName in filesToUpdate)
                {
                    string remoteUrl  = rawBaseUrl.TrimEnd('/') + "/" + fileName;
                    string localFile  = Path.Combine(localFolder, fileName);
                    string backupFile = localFile + ".bak";

                    try
                    {
                        string content = Download(remoteUrl);

                        // Бекап старого файлу
                        if (File.Exists(localFile))
                        {
                            File.Copy(localFile, backupFile, overwrite: true);
                        }

                        File.WriteAllText(localFile, content, Encoding.UTF8);
                        result.UpdatedFiles.Add(fileName);

                        // Верифікуємо хеш після запису
                        if (remoteHashes.ContainsKey(fileName))
                        {
                            string written = Sha256File(localFile);
                            string expected = remoteHashes[fileName].ToLowerInvariant();
                            if (written != expected)
                            {
                                // Відновлюємо бекап
                                if (File.Exists(backupFile))
                                    File.Copy(backupFile, localFile, overwrite: true);
                                string err = $"{fileName}: хеш після запису не збігається — відновлено бекап";
                                result.Errors.Add(err);
                                Log(logPath, "ERROR", err);
                                result.UpdatedFiles.Remove(fileName);
                                continue;
                            }
                        }

                        Log(logPath, "INFO", $"✓ {fileName} оновлено");
                    }
                    catch (Exception ex)
                    {
                        string err = $"Помилка завантаження {fileName}: {ex.Message}";
                        result.Errors.Add(err);
                        Log(logPath, "ERROR", err);

                        // Відновлюємо бекап якщо є
                        if (File.Exists(backupFile) && File.Exists(localFile))
                        {
                            try { File.Copy(backupFile, localFile, overwrite: true); }
                            catch { }
                        }
                    }
                }

                // ── 8. Зберігаємо нову version.json локально ─────────────
                if (result.UpdatedFiles.Count > 0)
                {
                    try
                    {
                        File.WriteAllText(localVersionFile, versionJson, Encoding.UTF8);
                        Log(logPath, "INFO", $"version.json збережено локально (v{remoteVersion})");
                    }
                    catch (Exception ex)
                    {
                        Log(logPath, "WARN", $"Не вдалось зберегти version.json: {ex.Message}");
                    }
                }

                // ── 9. Підсумок ───────────────────────────────────────────
                result.Success = result.Errors.Count == 0 || result.UpdatedFiles.Count > 0;
                result.Updated = result.UpdatedFiles.Count > 0;

                if (result.Updated)
                {
                    string notes = string.IsNullOrEmpty(remoteNotes) ? "" : $" | {remoteNotes}";
                    result.Message = $"✅ Оновлено до v{remoteVersion}{notes}: {string.Join(", ", result.UpdatedFiles)}";
                    if (result.Errors.Count > 0)
                        result.Message += $" (з {result.Errors.Count} помилками)";
                }
                else
                {
                    result.Message = $"⚠️ Оновлення невдале: {string.Join("; ", result.Errors)}";
                }

                Log(logPath, "INFO", result.Message);
                Log(logPath, "INFO", "=== VideoAlertUpdater END ===");
            }
            catch (Exception ex)
            {
                return Fail(result, $"Критична помилка: {ex.Message}", logPath);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════
        private static string Download(string url)
        {
            var wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            wc.Headers[HttpRequestHeader.UserAgent] = $"VideoAlertUpdater/{UpdaterInfo.DLL_VERSION}";
            // Таймаут через окремий thread (WebClient не має вбудованого таймауту)
            string result = null;
            Exception error = null;
            var t = new Thread(() =>
            {
                try   { result = wc.DownloadString(url); }
                catch (Exception ex) { error = ex; }
            });
            t.Start();
            if (!t.Join(15000))
            {
                wc.CancelAsync();
                throw new TimeoutException($"Таймаут 15с при завантаженні {url}");
            }
            if (error != null) throw error;
            return result;
        }

        private static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs  = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(64);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Порівнює версії у форматі "MAJOR.MINOR.PATCH"
        // Повертає: >0 якщо a>b, 0 якщо рівні, <0 якщо a<b
        private static int CompareVersions(string a, string b)
        {
            int[] pa = ParseVer(a), pb = ParseVer(b);
            for (int i = 0; i < 3; i++)
            {
                int diff = pa[i].CompareTo(pb[i]);
                if (diff != 0) return diff;
            }
            return 0;
        }

        private static int[] ParseVer(string v)
        {
            if (string.IsNullOrEmpty(v)) return new[] { 0, 0, 0 };
            string[] parts = v.Trim().TrimStart('v').Split('.');
            int[] r = new int[3];
            for (int i = 0; i < 3 && i < parts.Length; i++)
                int.TryParse(parts[i], out r[i]);
            return r;
        }

        // Мінімальний JSON-парсер для простих рядкових полів
        private static string ParseJsonField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            // "key": "value"
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + search.Length);
            if (colon < 0) return null;
            int start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;
            int end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }

        // Парсить блок "hashes": { "index.html": "abc...", "overlay.html": "def..." }
        private static Dictionary<string, string> ParseHashesBlock(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json)) return dict;

            int hashIdx = json.IndexOf("\"hashes\"", StringComparison.OrdinalIgnoreCase);
            if (hashIdx < 0) return dict;

            int braceOpen = json.IndexOf('{', hashIdx + 8);
            if (braceOpen < 0) return dict;
            int braceClose = json.IndexOf('}', braceOpen + 1);
            if (braceClose < 0) return dict;

            string block = json.Substring(braceOpen + 1, braceClose - braceOpen - 1);

            // Парсимо "key": "value" пари
            int pos = 0;
            while (pos < block.Length)
            {
                int ks = block.IndexOf('"', pos);
                if (ks < 0) break;
                int ke = block.IndexOf('"', ks + 1);
                if (ke < 0) break;
                string k = block.Substring(ks + 1, ke - ks - 1);

                int colon = block.IndexOf(':', ke + 1);
                if (colon < 0) break;
                int vs = block.IndexOf('"', colon + 1);
                if (vs < 0) break;
                int ve = block.IndexOf('"', vs + 1);
                if (ve < 0) break;
                string v = block.Substring(vs + 1, ve - vs - 1);

                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                    dict[k] = v;

                pos = ve + 1;
            }

            return dict;
        }

        private static UpdateResult Fail(UpdateResult r, string msg, string logPath)
        {
            r.Success = false;
            r.Updated = false;
            r.Message = msg;
            r.Errors.Add(msg);
            Log(logPath, "ERROR", msg);
            return r;
        }

        private static void Log(string logPath, string level, string text)
        {
            if (string.IsNullOrEmpty(logPath)) return;
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [UPDATER|{level}] {text}";
                File.AppendAllText(logPath, line + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }
}
