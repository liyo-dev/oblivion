# Script de Analisis Detallado del Flujo de Profile
# Identifica sistemas que acceden a GameBootService.Profile y analiza cuando lo hacen

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ANALISIS DE FLUJO DE PROFILE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptsPath = "Assets\Scripts"
$results = @()

# Buscar todos los archivos que accedan a GameBootService.Profile
$files = Get-ChildItem -Path $scriptsPath -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | 
    Where-Object { (Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue) -match 'GameBootService\.Profile' }

Write-Host "Archivos encontrados: $($files.Count)" -ForegroundColor Yellow
Write-Host ""

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    # Extraer nombre de clase
    if ($content -match 'class\s+(\w+)') {
        $className = $Matches[1]
    } else {
        $className = $file.BaseName
    }
    
    # Patron de suscripcion
    $subscribesToOnProfileReady = $content -match 'GameBootService\.OnProfileReady\s*\+='
    $registersInDiagnostics = $content -match 'ProfileReadyDiagnostics\.RegisterSubscriber'
    $isStaticClass = $content -match 'static\s+class'
    
    # Lugares donde accede al Profile
    $accessInAwake = ($content -match 'void\s+Awake\s*\([^)]*\)[^{]*\{[^}]*GameBootService\.Profile') -or 
                     ($content -match 'private\s+void\s+Awake\s*\([^)]*\)[^{]*\{[^}]*GameBootService\.Profile')
    
    $accessInStart = ($content -match 'void\s+Start\s*\([^)]*\)[^{]*\{[^}]*GameBootService\.Profile') -or 
                     ($content -match 'private\s+void\s+Start\s*\([^)]*\)[^{]*\{[^}]*GameBootService\.Profile')
    
    $accessInOnEnable = ($content -match 'void\s+OnEnable\s*\([^)]*\)[^{]*\{[^}]*GameBootService\.Profile') -or 
                        ($content -match 'private\s+void\s+OnEnable\s*\([^)]*\)[^{]*\{[^}]*GameBootService\.Profile')
    
    # Verificar si tiene patron HandleProfileReady o similar
    $hasProfileReadyHandler = $content -match '(Handle|On)ProfileReady'
    
    # Determinar estado
    $status = "OK"
    $issues = @()
    
    if (-not $isStaticClass) {
        if ($accessInAwake -or $accessInStart -or $accessInOnEnable) {
            if (-not $subscribesToOnProfileReady -and -not $hasProfileReadyHandler) {
                $status = "PROBLEMA"
                if ($accessInAwake) { $issues += "Acceso en Awake sin suscripcion" }
                if ($accessInStart) { $issues += "Acceso en Start sin suscripcion" }
                if ($accessInOnEnable) { $issues += "Acceso en OnEnable sin suscripcion" }
            }
        }
    }
    
    $result = [PSCustomObject]@{
        Clase = $className
        Archivo = $file.Name
        Path = $file.FullName.Replace((Get-Location).Path + '\', '')
        SuscribeOnProfileReady = $subscribesToOnProfileReady
        RegistraEnDiagnostics = $registersInDiagnostics
        TieneHandlerProfileReady = $hasProfileReadyHandler
        AccesoEnAwake = $accessInAwake
        AccesoEnStart = $accessInStart
        AccesoEnOnEnable = $accessInOnEnable
        EsClaseEstatica = $isStaticClass
        Estado = $status
        Problemas = ($issues -join "; ")
    }
    
    $results += $result
    
    # Mostrar resultado
    if ($status -eq "PROBLEMA") {
        Write-Host "❌ PROBLEMA: $className" -ForegroundColor Red
    } else {
        Write-Host "✅ OK: $className" -ForegroundColor Green
    }
    
    Write-Host "   Archivo: $($file.Name)" -ForegroundColor Gray
    Write-Host "   Se suscribe a OnProfileReady: $subscribesToOnProfileReady" -ForegroundColor $(if($subscribesToOnProfileReady){"Green"}else{"Yellow"})
    Write-Host "   Tiene handler ProfileReady: $hasProfileReadyHandler" -ForegroundColor $(if($hasProfileReadyHandler){"Green"}else{"Yellow"})
    Write-Host "   Accede en Awake: $accessInAwake" -ForegroundColor $(if($accessInAwake){"Yellow"}else{"Gray"})
    Write-Host "   Accede en Start: $accessInStart" -ForegroundColor $(if($accessInStart){"Yellow"}else{"Gray"})
    Write-Host "   Accede en OnEnable: $accessInOnEnable" -ForegroundColor $(if($accessInOnEnable){"Yellow"}else{"Gray"})
    
    if ($issues.Count -gt 0) {
        Write-Host "   ⚠️ Problemas: $($issues -join ', ')" -ForegroundColor Red
    }
    Write-Host ""
}

# Resumen
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESUMEN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$problemCount = ($results | Where-Object { $_.Estado -eq "PROBLEMA" }).Count
$okCount = ($results | Where-Object { $_.Estado -eq "OK" }).Count

Write-Host "Total archivos analizados: $($results.Count)" -ForegroundColor White
Write-Host "Sistemas OK: $okCount" -ForegroundColor Green
Write-Host "Sistemas con problemas: $problemCount" -ForegroundColor $(if($problemCount -gt 0){"Red"}else{"Green"})
Write-Host ""

if ($problemCount -gt 0) {
    Write-Host "SISTEMAS QUE REQUIEREN ATENCION:" -ForegroundColor Red
    Write-Host ""
    
    $problems = $results | Where-Object { $_.Estado -eq "PROBLEMA" }
    foreach ($p in $problems) {
        Write-Host "  📛 $($p.Clase)" -ForegroundColor Yellow
        Write-Host "     Archivo: $($p.Archivo)" -ForegroundColor Gray
        Write-Host "     Problemas: $($p.Problemas)" -ForegroundColor Red
        Write-Host ""
    }
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "RECOMENDACIONES" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Todos los sistemas que acceden a GameBootService.Profile deben:" -ForegroundColor White
    Write-Host "  1. Suscribirse a GameBootService.OnProfileReady en OnEnable" -ForegroundColor Yellow
    Write-Host "  2. Registrarse en ProfileReadyDiagnostics" -ForegroundColor Yellow
    Write-Host "  3. Implementar un metodo HandleProfileReady" -ForegroundColor Yellow
    Write-Host "  4. NO acceder al Profile en Awake/Start/OnEnable directamente" -ForegroundColor Yellow
    Write-Host ""
    
    exit 1
} else {
    Write-Host "Todos los sistemas estan correctamente configurados!" -ForegroundColor Green
    Write-Host ""
    exit 0
}
