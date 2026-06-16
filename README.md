# VideoAlertUpdater — Система авто-оновлень

Дозволяє розповсюдити `index.html` + `overlay.html` серед користувачів
і автоматично оновлювати їхні копії через GitHub при кожному старті Streamer.bot.

---

## 📦 Що входить у пакет

```
VideoAlertUpdater.dll       ← DLL для Streamer.bot
SB_Action_AutoUpdateFiles.cs ← C# код для SB action (скопіюй вміст)
update-version.ps1          ← PowerShell скрипт для тебе (перед git push)
version.json                ← Шаблон файлу версії (завантажити на GitHub)
README.md                   ← Ця інструкція
```

---

## 🛠️ НАЛАШТУВАННЯ ДЛЯ ТЕБЕ (автора)

### Крок 1 — Створи GitHub репозиторій

1. Зайди на github.com → New repository
2. Назви, наприклад: `video-alert-overlay`
3. Зроби **Public**
4. Структура репо має бути такою:
   ```
   /reward/
       index.html
       overlay.html
       version.json
   ```

### Крок 2 — Завантажи файли на GitHub

```bash
git clone https://github.com/ВАШ_НІКНЕЙМ/video-alert-overlay
cd video-alert-overlay
mkdir reward
# скопіюй index.html і overlay.html в папку reward/
cp /шлях/до/index.html   reward/
cp /шлях/до/overlay.html reward/
cp /шлях/до/version.json reward/
```

### Крок 3 — Запусти скрипт хешування (ЗАВЖДИ перед git push!)

```powershell
# Відкрий PowerShell в папці reward/
.\update-version.ps1 -Folder ".\reward" -Version "1.0.0" -Notes "Перший реліз"
```

Скрипт сам порахує SHA-256 хеші і запише у `version.json`.

### Крок 4 — Завантаж на GitHub

```bash
git add reward/
git commit -m "v1.0.0 - перший реліз"
git push
```

### Як оновлювати файли в майбутньому

```powershell
# 1. Внеси зміни в index.html і/або overlay.html
# 2. Запусти скрипт:
.\update-version.ps1 -Folder ".\reward" -Version "1.1.0" -Notes "Додали нову функцію"
# 3. Зроби git add + commit + push
```

Все! Юзери при наступному старті SB автоматично отримають оновлення.

---

## 👥 НАЛАШТУВАННЯ ДЛЯ КОРИСТУВАЧІВ

### Що потрібно передати юзеру:

```
VideoAlertUpdater.dll
index.html
overlay.html
```

### Крок 1 — Покласти DLL в Streamer.bot

Скопіюй `VideoAlertUpdater.dll` в:
```
C:\Users\ІМ'Я\AppData\Roaming\Streamer.bot\dlls\
```
*(або Documents\Streamer.bot\dlls\ — залежить від версії)*

### Крок 2 — Підключити DLL в Streamer.bot

1. Відкрий Streamer.bot
2. Зайди у **Settings → References** (або **C# References**)
3. Натисни **Add** → знайди `VideoAlertUpdater.dll`
4. Збережи

### Крок 3 — Створити action AutoUpdateFiles

1. В Streamer.bot: **Actions → Add Action**
2. Назва: `AutoUpdateFiles`
3. Sub-action: **Execute C# Code**
4. Відкрий файл `SB_Action_AutoUpdateFiles.cs` — скопіюй весь вміст у поле коду
5. **Відредагуй 3 рядки на початку:**

```csharp
// URL до GitHub репо (змінити на реальний!)
const string GITHUB_RAW_URL = "https://raw.githubusercontent.com/YOUR_USERNAME/YOUR_REPO/main/reward";

// Де лежать твої файли
const string LOCAL_FOLDER = @"D:\Program\obs\Obs Twich Reward\reward";

// Лог
const string LOG_PATH = @"D:\Program\obs\Obs Twich Reward\alerts.log";
```

6. Натисни **Compile** — має бути без помилок
7. Збережи action

### Крок 4 — Додати тригер

1. В тому ж action → **Triggers → Add Trigger**
2. Вибери: **Core → Streamer.bot Started**
3. Збережи

### Готово! 🎉

Тепер при кожному запуску Streamer.bot:
- DLL перевіряє `version.json` на GitHub
- Порівнює версію та SHA-256 хеші файлів
- Якщо є зміни — скачує нові `index.html` та `overlay.html`
- Рефрешить Browser Source в OBS
- Дані алертів (localStorage) **не зачіпаються**

---

## 🔍 Як читати логи

```
2025-06-16 10:00:01 [UPDATER|INFO] === VideoAlertUpdater v1.0.0 START ===
2025-06-16 10:00:01 [UPDATER|INFO] Версія локальна=1.0.0 | remote=1.1.0
2025-06-16 10:00:01 [UPDATER|INFO] index.html: нова версія (1.0.0→1.1.0)
2025-06-16 10:00:02 [UPDATER|INFO] ✓ index.html оновлено
2025-06-16 10:00:02 [UPDATER|INFO] ✅ Оновлено до v1.1.0: index.html
```

---

## ❓ Часті питання

**Q: Чи видаляються налаштування алертів при оновленні?**
A: Ні. Налаштування зберігаються в localStorage браузера — DLL тільки замінює HTML/JS файли.

**Q: Що якщо немає інтернету?**
A: DLL пропускає оновлення і логує помилку. Streamer.bot продовжує працювати з поточними файлами.

**Q: Що якщо GitHub недоступний?**
A: Те саме — помилка в лог, SB продовжує нормальну роботу.

**Q: Де зберігається бекап старих файлів?**
A: Поруч з оригіналом: `index.html.bak` та `overlay.html.bak`. Автоматично відновлюються якщо хеш не збігається після завантаження.

**Q: Чи можна запустити оновлення вручну?**
A: Так! В Streamer.bot знайди action `AutoUpdateFiles` і клікни Run.

---

## 📋 Формат version.json

```json
{
  "version": "1.2.0",
  "date": "2025-06-16",
  "notes": "Опис змін для логів",
  "hashes": {
    "index.html":   "a1b2c3d4e5f6...64символи...",
    "overlay.html": "f6e5d4c3b2a1...64символи..."
  }
}
```

**Важливо:** завжди запускай `update-version.ps1` перед push — він сам рахує хеші.

---

*VideoAlertUpdater v1.0.0*
