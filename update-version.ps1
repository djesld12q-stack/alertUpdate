# ╔══════════════════════════════════════════════════════════╗
# ║  update-version.ps1                                      ║
# ║  Запускай цей скрипт ПЕРЕД тим як зробити git push       ║
# ║  Він автоматично рахує SHA-256 хеші і оновлює version.json║
# ╚══════════════════════════════════════════════════════════╝

param(
    [string]$Version   = "",          # якщо пусто — питає
    [string]$Notes     = "",          # опис змін
    [string]$Folder    = "."          # де лежать файли (за замовч. поточна папка)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$indexFile   = Join-Path $Folder "index.html"
$overlayFile = Join-Path $Folder "overlay.html"
$versionFile = Join-Path $Folder "version.json"

# ── Перевірка ────────────────────────────────────────────────────────────
if (-not (Test-Path $indexFile)) {
    Write-Host "ПОМИЛКА: index.html не знайдено в $Folder" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $overlayFile)) {
    Write-Host "ПОМИЛКА: overlay.html не знайдено в $Folder" -ForegroundColor Red
    exit 1
}

# ── Поточна версія ───────────────────────────────────────────────────────
$currentVersion = "0.0.0"
if (Test-Path $versionFile) {
    $json = Get-Content $versionFile -Raw | ConvertFrom-Json
    $currentVersion = $json.version
}
Write-Host "Поточна версія: $currentVersion" -ForegroundColor Cyan

# ── Запитуємо нову версію якщо не передана ──────────────────────────────
if ([string]::IsNullOrEmpty($Version)) {
    $Version = Read-Host "Нова версія (зараз $currentVersion, Enter = без змін)"
    if ([string]::IsNullOrEmpty($Version)) { $Version = $currentVersion }
}

if ([string]::IsNullOrEmpty($Notes)) {
    $Notes = Read-Host "Що змінилось (опис, Enter = пропустити)"
}

# ── Рахуємо SHA-256 ──────────────────────────────────────────────────────
function Get-FileHash256 { param($Path)
    $hash = Get-FileHash -Path $Path -Algorithm SHA256
    return $hash.Hash.ToLower()
}

$hashIndex   = Get-FileHash256 $indexFile
$hashOverlay = Get-FileHash256 $overlayFile

Write-Host ""
Write-Host "index.html   SHA256: $hashIndex"   -ForegroundColor Gray
Write-Host "overlay.html SHA256: $hashOverlay" -ForegroundColor Gray

# ── Будуємо version.json ─────────────────────────────────────────────────
$versionObj = [PSCustomObject]@{
    version = $Version
    date    = (Get-Date -Format "yyyy-MM-dd")
    notes   = $Notes
    hashes  = [PSCustomObject]@{
        "index.html"   = $hashIndex
        "overlay.html" = $hashOverlay
    }
}

$versionObj | ConvertTo-Json -Depth 3 | Set-Content $versionFile -Encoding UTF8

Write-Host ""
Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  version.json оновлено!                ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
Write-Host "Версія: $Version"    -ForegroundColor Green
Write-Host "Дата:   $(Get-Date -Format 'yyyy-MM-dd')" -ForegroundColor Green
if ($Notes) { Write-Host "Нотатки: $Notes" -ForegroundColor Green }
Write-Host ""
Write-Host "Тепер зроби:" -ForegroundColor Yellow
Write-Host "  git add index.html overlay.html version.json" -ForegroundColor White
Write-Host "  git commit -m `"v$Version - $Notes`"" -ForegroundColor White
Write-Host "  git push" -ForegroundColor White
Write-Host ""
