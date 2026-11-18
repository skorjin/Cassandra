# Regenerates catalog/models.json. Sizes and SHA-256 of the large ONNX files come from the Hugging Face API
# (LFS object ids); the few small files are downloaded once and hashed here.
# Run this when a variant or a model version changes, then commit the result.
param(
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'catalog/models.json')
)

$ErrorActionPreference = 'Stop'

$variants = @(
    @{ id = 'fast'; name = 'Быстрая'; precision = 'int8'; suffix = '_quantized'; version = 1
       description = 'Квантованная (int8). Меньше размер и расход памяти, перевод немного проще.' },
    @{ id = 'accurate'; name = 'Точная'; precision = 'fp32'; suffix = ''; version = 1
       description = 'Полная точность (fp32). Лучше качество, в 4 раза больше размер и память.' }
)
$small = 'config.json', 'generation_config.json', 'source.spm', 'target.spm', 'vocab.json'
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('catalog-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp | Out-Null

function Get-SmallFileHash($url, $name) {
    $path = Join-Path $tmp $name
    Invoke-WebRequest -Uri $url -OutFile $path -UseBasicParsing
    (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}

try {
    $result = foreach ($v in $variants) {
        $files = foreach ($pair in 'en-ru', 'ru-en') {
            $base = "https://huggingface.co/Xenova/opus-mt-$pair/resolve/main"
            $tree = Invoke-RestMethod "https://huggingface.co/api/models/Xenova/opus-mt-$pair/tree/main?recursive=true"
            foreach ($f in $small) {
                $item = $tree | Where-Object { $_.path -eq $f }
                [ordered]@{ path = "$pair/$f"; url = "$base/$f"; size = [long]$item.size; sha256 = (Get-SmallFileHash "$base/$f" "$pair-$f") }
            }
            foreach ($f in 'encoder_model', 'decoder_model') {
                $remote = "onnx/$f$($v.suffix).onnx"
                $item = $tree | Where-Object { $_.path -eq $remote }
                if (-not $item.lfs.oid) { throw "No LFS hash for $remote" }
                [ordered]@{ path = "$pair/$f.onnx"; url = "$base/$remote"; size = [long]$item.size; sha256 = $item.lfs.oid }
            }
        }
        [ordered]@{ id = $v.id; name = $v.name; precision = $v.precision; description = $v.description; version = $v.version; files = @($files) }
    }

    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    [ordered]@{ schema = 1; variants = @($result) } | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 $Out
    Write-Host "Wrote $Out"
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
