# AI-assisted: written with Claude. See docs/AI_USE.md
<#
  Локальний сервер для веб-збірки (index.html із файлу не запускається: браузер не дає читати .wasm).
  Запуск з папки репозиторію:
      powershell -ExecutionPolicy Bypass -File tools\serve_web.ps1
  Потім відкрий http://localhost:8085/ у Chrome або Edge. Зупинити: Ctrl+C.
  Інший порт або папка:  -Port 9000  -Root D:\somewhere\Web
  Файли .gz / .br віддаються з правильним заголовком Content-Encoding; файли .unityweb (Decompression Fallback) розпаковує сама гра.
#>
param(
    [string]$Root = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Builds\Web'),
    [int]$Port = 8085
)

if (-not (Test-Path (Join-Path $Root 'index.html'))) {
    Write-Host "Немає $Root\index.html. Спершу збери гру: Unity -> Moon -> Збірка для браузера (WebGL)."
    exit 1
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Гра: http://localhost:$Port/   (папка $Root, Ctrl+C — зупинити)"

$types = @{ '.html' = 'text/html'; '.js' = 'application/javascript'; '.wasm' = 'application/wasm'; '.data' = 'application/octet-stream';
            '.css' = 'text/css'; '.json' = 'application/json'; '.png' = 'image/png'; '.ico' = 'image/x-icon'; '.svg' = 'image/svg+xml' }
try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        try {
            $path = [Uri]::UnescapeDataString($ctx.Request.Url.LocalPath.TrimStart('/'))
            if ($path -eq '') { $path = 'index.html' }
            $file = Join-Path $Root $path
            # не віддаємо нічого поза папкою збірки
            if ((Test-Path -LiteralPath $file -PathType Leaf) -and ([IO.Path]::GetFullPath($file).StartsWith([IO.Path]::GetFullPath($Root)))) {
                $name = $file; $enc = $null
                if ($file.EndsWith('.br')) { $enc = 'br'; $name = $file.Substring(0, $file.Length - 3) }
                elseif ($file.EndsWith('.gz')) { $enc = 'gzip'; $name = $file.Substring(0, $file.Length - 3) }
                $ext = [IO.Path]::GetExtension($name).ToLower()
                $ctx.Response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
                if ($enc) { $ctx.Response.AddHeader('Content-Encoding', $enc) }
                $bytes = [IO.File]::ReadAllBytes($file)
                $ctx.Response.ContentLength64 = $bytes.Length
                $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            } else { $ctx.Response.StatusCode = 404 }
        } catch { Write-Host "помилка: $_" }
        $ctx.Response.Close()
    }
} finally {
    $listener.Stop()
}
