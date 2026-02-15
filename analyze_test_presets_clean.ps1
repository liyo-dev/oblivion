# Script para Verificar y Limpiar TODOS los Presets de Testeo

Write-Host "ANALIZADOR DE PRESETS DE TESTEO" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$presetsPath = "C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts\Player\SO"
$presets = Get-ChildItem -Path $presetsPath -Filter "PlayerPreset_Test*.asset"

$problematicEvents = @(
    "EXIT_FROM_WOODS_ESTELA_received",
    "BATTLE_START"
)

Write-Host "Presets encontrados: $($presets.Count)" -ForegroundColor Yellow
Write-Host ""

$totalProblems = 0

foreach ($preset in $presets) {
    $content = Get-Content $preset.FullName -Raw
    $hasProblems = $false
    $problems = @()
    
    foreach ($event in $problematicEvents) {
        if ($content -match $event) {
            $hasProblems = $true
            $problems += $event
        }
    }
    
    if ($hasProblems) {
        $totalProblems++
        Write-Host "PROBLEMA: $($preset.Name)" -ForegroundColor Yellow
        foreach ($problem in $problems) {
            Write-Host "  - Tiene flag: $problem" -ForegroundColor Red
        }
        Write-Host ""
    } else {
        Write-Host "OK: $($preset.Name)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "RESUMEN:" -ForegroundColor Cyan
Write-Host "Total presets: $($presets.Count)" -ForegroundColor Gray
Write-Host "Con problemas: $totalProblems" -ForegroundColor $(if ($totalProblems -gt 0) { "Red" } else { "Green" })
Write-Host "Limpios: $($presets.Count - $totalProblems)" -ForegroundColor Green

if ($totalProblems -gt 0) {
    Write-Host ""
    Write-Host "RECOMENDACION:" -ForegroundColor Yellow
    Write-Host "Los presets con flags de eventos trigger NO deben usarse para testear esos triggers" -ForegroundColor Gray
    Write-Host "Mejor crear presets ANTES de llegar a cada trigger" -ForegroundColor Gray
}
