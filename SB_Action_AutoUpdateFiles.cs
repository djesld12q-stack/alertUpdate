// ╔═══════════════════════════════════════════════════════════════════╗
// ║  ACTION: "AutoUpdateFiles"                                        ║
// ║  Trigger: Core → Streamer.bot Started                             ║
// ║  Sub-action: Execute C# Code                                      ║
// ║                                                                   ║
// ║  ЗАЛЕЖНІСТЬ: VideoAlertUpdater.dll поклади у папку                ║
// ║  Documents\Streamer.bot\dlls\  або поруч з index.html             ║
// ║  і підключи через References у Streamer.bot                       ║
// ╚═══════════════════════════════════════════════════════════════════╝

using System;
using System.IO;
using VideoAlertUpdater;

public class CPHInline
{
    // ══════════════════════════════════════════════════════════════════
    //  ⚙️  НАЛАШТУВАННЯ — відредагуй ці 3 рядки під себе
    // ══════════════════════════════════════════════════════════════════

    // URL до твоєї папки на GitHub (raw.githubusercontent.com)
    // Формат: https://raw.githubusercontent.com/ІМ'Я/РЕПО/ГІЛКА/ПАПКА
    const string GITHUB_RAW_URL = "https://raw.githubusercontent.com/YOUR_USERNAME/YOUR_REPO/main/reward";

    // Локальна папка де лежать index.html і overlay.html
    const string LOCAL_FOLDER   = @"D:\Program\obs\Obs Twich Reward\reward";

    // Шлях до лог-файлу (той самий що в інших діях)
    const string LOG_PATH       = @"D:\Program\obs\Obs Twich Reward\alerts.log";

    // ══════════════════════════════════════════════════════════════════

    public bool Execute()
    {
        CPH.LogInfo("AutoUpdateFiles: перевірка оновлень...");

        try
        {
            var result = Updater.CheckAndUpdate(
                rawBaseUrl:   GITHUB_RAW_URL,
                localFolder:  LOCAL_FOLDER,
                logPath:      LOG_PATH
            );

            if (!result.Success)
            {
                CPH.LogError("AutoUpdateFiles: " + result.Message);
                // Не зупиняємо запуск SB — просто логуємо помилку
                return true;
            }

            if (result.Updated)
            {
                CPH.LogInfo("AutoUpdateFiles: ✅ " + result.Message);

                // Рефрешимо Browser Source в OBS після оновлення файлів
                // (дочекаємось підключення OBS — макс 10с)
                int waited = 0;
                while (!CPH.ObsIsConnected() && waited < 10000)
                {
                    System.Threading.Thread.Sleep(500);
                    waited += 500;
                }

                if (CPH.ObsIsConnected())
                {
                    // Рефрешимо overlay (Browser Source в OBS)
                    // Зміни назву "AlertReward" на точну назву твого Browser Source
                    string[] sources = { "AlertReward" };
                    foreach (string src in sources)
                    {
                        try
                        {
                            string req  = "{\"inputName\":\"" + src + "\",\"propertyName\":\"refreshnocache\"}";
                            string resp = CPH.ObsSendRaw("PressInputPropertiesButton", req);
                            CPH.LogInfo("AutoUpdateFiles: рефреш '" + src + "' → " + (resp ?? "null"));
                        }
                        catch (Exception ex)
                        {
                            CPH.LogWarn("AutoUpdateFiles: рефреш '" + src + "' FAIL — " + ex.Message);
                        }
                    }
                }
                else
                {
                    CPH.LogWarn("AutoUpdateFiles: OBS не підключений — рефреш пропущено");
                }
            }
            else
            {
                CPH.LogInfo("AutoUpdateFiles: " + result.Message);
            }
        }
        catch (Exception ex)
        {
            // DLL не знайдена або інша критична помилка
            CPH.LogError("AutoUpdateFiles: критична помилка — " + ex.Message);
            CPH.LogError("Переконайся що VideoAlertUpdater.dll підключена в References");
        }

        return true;
    }
}
