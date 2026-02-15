# Script de Validación: Sistemas con Variables Estáticas

# Buscar TODOS los archivos C# que declaran variables estáticas
Write-Host "🔍 Buscando sistemas con variables estáticas..." -ForegroundColor Cyan

$projectPath = "C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts"

# Buscar archivos con "public static" o "private static"
$results = @()

Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # Verificar si tiene variables estáticas
    if ($content -match "(?:public|private)\s+static\s+(?!class|void|bool|int|string|float|readonly\s+string\[\]|IEnumerator)" -and 
        $content -notmatch "RuntimeInitializeOnLoadMethod") {
        
        $relativePath = $_.FullName.Replace($projectPath, "Scripts")
        $results += [PSCustomObject]@{
            File = $relativePath
            HasResetMethod = $content -match "RuntimeInitializeOnLoadMethod"
        }
    }
}

Write-Host ""
Write-Host "📊 RESULTADOS:" -ForegroundColor Yellow
Write-Host "===============" -ForegroundColor Yellow

if ($results.Count -eq 0) {
    Write-Host "✅ No se encontraron sistemas con variables estáticas sin reset" -ForegroundColor Green
} else {
    Write-Host "⚠️  Se encontraron $($results.Count) archivos con variables estáticas:" -ForegroundColor Yellow
    Write-Host ""
    
    $withReset = $results | Where-Object { $_.HasResetMethod -eq $true }
    $withoutReset = $results | Where-Object { $_.HasResetMethod -eq $false }
    
    if ($withReset) {
        Write-Host "✅ CON reset ($($withReset.Count)):" -ForegroundColor Green
        $withReset | ForEach-Object { Write-Host "   - $($_.File)" -ForegroundColor Gray }
        Write-Host ""
    }
    
    if ($withoutReset) {
        Write-Host "❌ SIN reset ($($withoutReset.Count)) - REQUIERE ATENCIÓN:" -ForegroundColor Red
        $withoutReset | ForEach-Object { Write-Host "   - $($_.File)" -ForegroundColor Yellow }
    }
}

Write-Host ""
Write-Host "✅ Validación completada" -ForegroundColor Green
