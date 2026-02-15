# Script de Análisis de Suscripciones a OnProfileReady
# Detecta automáticamente sistemas que accedan al perfil pero no se suscriban correctamente

param(
    [switch]$Verbose,
    [switch]$ShowCorrect,
    [string]$Output = "analysis_report.txt"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ANÁLISIS DE SUSCRIPCIONES OnProfileReady" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$scriptsPath = "Assets\Scripts"
$issues = @()
$correctSystems = @()
$summary = @{
    TotalFiles = 0
    FilesWithProfileAccess = 0
    ProblematicSystems = 0
    CorrectSystems = 0
}

# Buscar todos los archivos .cs
$csFiles = Get-ChildItem -Path $scriptsPath -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue

if ($csFiles.Count -eq 0) {
    Write-Host "❌ No se encontraron archivos .cs en $scriptsPath" -ForegroundColor Red
    exit 1
}

$summary.TotalFiles = $csFiles.Count

Write-Host "📂 Escaneando $($csFiles.Count) archivos C#..." -ForegroundColor Yellow
Write-Host ""

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    # Verificar si accede al perfil
    $accessesProfile = $content -match 'GameBootService\.Profile' -or 
                       $content -match '\.GetActivePresetResolved\(\)'
    
    if (-not $accessesProfile) { continue }
    
    $summary.FilesWithProfileAccess++
    
    # Extraer nombre de la clase
    if ($content -match 'class\s+(\w+)\s*:.*MonoBehaviour') {
        $className = $Matches[1]
    } elseif ($content -match 'class\s+(\w+)') {
        $className = $Matches[1]
    } else {
        $className = $file.BaseName
    }
    
    # Verificar patrones de suscripción correcta
    $subscribesInOnEnable = $content -match 'GameBootService\.OnProfileReady\s*\+=' -and $content -match 'void\s+OnEnable'
    $unsubscribesInOnDisable = $content -match 'GameBootService\.OnProfileReady\s*-=' -and $content -match 'void\s+OnDisable'
    $registersSubscriber = $content -match 'ProfileReadyDiagnostics\.RegisterSubscriber'
    $checksIsAvailable = $content -match 'GameBootService\.IsAvailable'
    
    # Verificar patrones problemáticos
    $hasAwake = $content -match 'void\s+Awake\s*\('
    $hasStart = $content -match 'void\s+Start\s*\('
    $accessInAwake = $hasAwake -and ($content -match 'Awake[\s\S]{0,500}GameBootService\.Profile')
    $accessInStart = $hasStart -and ($content -match 'Start[\s\S]{0,500}GameBootService\.Profile')
    
    # Verificar si es una clase estática (exenta)
    $isStaticClass = $content -match 'static\s+class\s+\w+'
    
    # Determinar estado
    $isCorrect = $subscribesInOnEnable -and $unsubscribesInOnDisable -and $registersSubscriber -and $checksIsAvailable
    $isProblemtic = ($accessInAwake -or $accessInStart) -and -not $subscribesInOnEnable
    
    $issue = [PSCustomObject]@{
        File = $file.Name
        Path = $file.FullName.Replace((Get-Location).Path + '\', '')
        Class = $className
        SubscribesInOnEnable = $subscribesInOnEnable
        UnsubscribesInOnDisable = $unsubscribesInOnDisable
        RegistersSubscriber = $registersSubscriber
        ChecksIsAvailable = $checksIsAvailable
        AccessInAwake = $accessInAwake
        AccessInStart = $accessInStart
        IsStaticClass = $isStaticClass
        IsCorrect = $isCorrect
        IsProblematic = $isProblemtic
        Score = 0
    }
    
    # Calcular score (0-100)
    $score = 0
    if ($subscribesInOnEnable) { $score += 30 }
    if ($unsubscribesInOnDisable) { $score += 20 }
    if ($registersSubscriber) { $score += 20 }
    if ($checksIsAvailable) { $score += 30 }
    if ($accessInAwake) { $score -= 50 }
    if ($accessInStart -and -not $subscribesInOnEnable) { $score -= 30 }
    if ($isStaticClass) { $score = 100 } # Clases estáticas son OK
    
    $issue.Score = [Math]::Max(0, [Math]::Min(100, $score))
    
    if ($issue.Score -lt 70 -and -not $isStaticClass) {
        $issues += $issue
        $summary.ProblematicSystems++
    } else {
        $correctSystems += $issue
        $summary.CorrectSystems++
    }
}

# REPORTAR RESULTADOS
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RESUMEN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "📊 Archivos totales: $($summary.TotalFiles)" -ForegroundColor White
Write-Host "🔍 Con acceso al perfil: $($summary.FilesWithProfileAccess)" -ForegroundColor White
Write-Host "❌ Sistemas problemáticos: $($summary.ProblematicSystems)" -ForegroundColor $(if ($summary.ProblematicSystems -gt 0) { "Red" } else { "Green" })
Write-Host "✅ Sistemas correctos: $($summary.CorrectSystems)" -ForegroundColor Green
Write-Host ""

if ($issues.Count -gt 0) {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  ❌ SISTEMAS PROBLEMÁTICOS" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    
    foreach ($issue in ($issues | Sort-Object Score)) {
        $color = switch ($issue.Score) {
            { $_ -lt 30 } { "Red" }
            { $_ -lt 50 } { "Yellow" }
            default { "White" }
        }
        
        Write-Host "🔴 $($issue.Class) (Score: $($issue.Score)/100)" -ForegroundColor $color
        Write-Host "   📁 $($issue.Path)" -ForegroundColor Gray
        
        if (-not $issue.SubscribesInOnEnable) {
            Write-Host "   ❌ NO se suscribe en OnEnable()" -ForegroundColor Red
        }
        if (-not $issue.UnsubscribesInOnDisable) {
            Write-Host "   ⚠️  NO se desuscribe en OnDisable()" -ForegroundColor Yellow
        }
        if (-not $issue.RegistersSubscriber) {
            Write-Host "   ⚠️  NO registra en ProfileReadyDiagnostics" -ForegroundColor Yellow
        }
        if (-not $issue.ChecksIsAvailable) {
            Write-Host "   ⚠️  NO verifica GameBootService.IsAvailable" -ForegroundColor Yellow
        }
        if ($issue.AccessInAwake) {
            Write-Host "   🚨 Accede al perfil en Awake() - PELIGRO!" -ForegroundColor Red
        }
        if ($issue.AccessInStart) {
            Write-Host "   ⚠️  Accede al perfil en Start()" -ForegroundColor Yellow
        }
        
        Write-Host ""
    }
}

if ($ShowCorrect -and $correctSystems.Count -gt 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  ✅ SISTEMAS CORRECTOS" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    
    foreach ($system in ($correctSystems | Sort-Object Class)) {
        Write-Host "✅ $($system.Class) (Score: $($system.Score)/100)" -ForegroundColor Green
        if ($Verbose) {
            Write-Host "   📁 $($system.Path)" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# GENERAR ARCHIVO DE REPORTE
$reportContent = @"
========================================
  REPORTE DE ANÁLISIS OnProfileReady
========================================
Generado: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

RESUMEN:
- Archivos totales: $($summary.TotalFiles)
- Con acceso al perfil: $($summary.FilesWithProfileAccess)
- Sistemas problemáticos: $($summary.ProblematicSystems)
- Sistemas correctos: $($summary.CorrectSystems)

========================================
  SISTEMAS PROBLEMÁTICOS
========================================

"@

foreach ($issue in ($issues | Sort-Object Score)) {
    $reportContent += @"
[$($issue.Score)/100] $($issue.Class)
    Archivo: $($issue.Path)
    - Suscribe en OnEnable: $($issue.SubscribesInOnEnable)
    - Desuscribe en OnDisable: $($issue.UnsubscribesInOnDisable)
    - Registra en Diagnostics: $($issue.RegistersSubscriber)
    - Verifica IsAvailable: $($issue.ChecksIsAvailable)
    - Accede en Awake: $($issue.AccessInAwake)
    - Accede en Start: $($issue.AccessInStart)

"@
}

$reportContent += @"

========================================
  SISTEMAS CORRECTOS
========================================

"@

foreach ($system in ($correctSystems | Sort-Object Class)) {
    $reportContent += "✅ $($system.Class) ($($system.Score)/100)`n"
}

$reportContent | Out-File -FilePath $Output -Encoding UTF8
Write-Host "📄 Reporte guardado en: $Output" -ForegroundColor Cyan

# RECOMENDACIONES
if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  💡 RECOMENDACIONES" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Revisar los sistemas marcados como problemáticos" -ForegroundColor White
    Write-Host "2. Aplicar el template de GUIA_DIAGNOSTICO_PROFILE_READY.md" -ForegroundColor White
    Write-Host "3. Añadir los sistemas a ProfileReadyDiagnostics._expectedSubscribers" -ForegroundColor White
    Write-Host "4. Ejecutar pruebas desde Start y MainWorld para verificar" -ForegroundColor White
    Write-Host ""
    
    exit 1
} else {
    Write-Host "🎉 Todos los sistemas que acceden al perfil estan correctamente configurados!" -ForegroundColor Green
    exit 0
}
