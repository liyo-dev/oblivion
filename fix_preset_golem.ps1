# Script para Limpiar Flags de Eventos Narrativos en Preset

$presetPath = "C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts\Player\SO\PlayerPreset_Test_Golem_noderrotado.asset"

Write-Host "🔧 Limpiando preset de testeo del Golem..." -ForegroundColor Cyan
Write-Host "Archivo: $presetPath" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $presetPath)) {
    Write-Host "❌ ERROR: Preset no encontrado en la ruta especificada" -ForegroundColor Red
    exit 1
}

# Leer contenido
$content = Get-Content $presetPath -Raw

# Backup
$backupPath = $presetPath + ".backup"
Copy-Item $presetPath $backupPath -Force
Write-Host "💾 Backup creado: $backupPath" -ForegroundColor Green

# Eliminar el flag problemático EXIT_FROM_WOODS_ESTELA_received
$pattern = "  - key: __event_1db8d4ab-1b78-48f8-961e-9e0668aacf6e_EXIT_FROM_WOODS_ESTELA_received\r?\n    type: \r?\n    value: 1\r?\n"

if ($content -match $pattern) {
    Write-Host "🔍 Encontrado flag EXIT_FROM_WOODS_ESTELA_received - Eliminando..." -ForegroundColor Yellow
    $content = $content -replace $pattern, ""
    Write-Host "✅ Flag eliminado" -ForegroundColor Green
} else {
    Write-Host "⚠️  Flag EXIT_FROM_WOODS_ESTELA_received NO encontrado" -ForegroundColor Yellow
    Write-Host "   Puede que ya esté limpio o tenga formato diferente" -ForegroundColor Gray
}

# OPCIONAL: Limpiar todo el blackboard (empezar desde cero)
Write-Host ""
$response = Read-Host "¿Limpiar TODO el blackboard narrativo? (S/N)"

if ($response -eq "S" -or $response -eq "s") {
    # Buscar y reemplazar el blackboard completo con uno vacío
    $blackboardPattern = "  narrativeBlackboards:[\s\S]*?(?=  npcPositions:)"
    $emptyBlackboard = @"
  narrativeBlackboards:
  - graphLabel: Historia Principal
    blackboardData:
    - key: __currentNodeGuid
      type: 
      value: 
  - graphLabel: Misiones Secundarias
    blackboardData:
    - key: __currentNodeGuid
      type: 
      value: 
"@
    
    if ($content -match $blackboardPattern) {
        $content = $content -replace $blackboardPattern, $emptyBlackboard
        Write-Host "✅ Blackboard completamente limpiado" -ForegroundColor Green
    }
}

# Guardar
Set-Content $presetPath $content -NoNewline
Write-Host ""
Write-Host "✅ Preset actualizado exitosamente" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Próximos pasos:" -ForegroundColor Cyan
Write-Host "  1. Volver a Unity (recargará el asset)" -ForegroundColor Gray
Write-Host "  2. Activar el preset de testeo" -ForegroundColor Gray
Write-Host "  3. Play desde MainWorld" -ForegroundColor Gray
Write-Host "  4. Ir al trigger del Golem" -ForegroundColor Gray
Write-Host "  5. Verificar que ahora funciona ✅" -ForegroundColor Gray
Write-Host ""
Write-Host "💾 Si algo sale mal, restaura desde: $backupPath" -ForegroundColor Yellow
