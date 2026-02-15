# Script Simple de Analisis de Suscripciones a OnProfileReady
Write-Host "Analizando suscripciones a OnProfileReady..." -ForegroundColor Cyan

$scriptsPath = "Assets\Scripts"
$problemFiles = @()

# Buscar archivos que accedan a GameBootService.Profile
$files = Get-ChildItem -Path $scriptsPath -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | 
    Where-Object { (Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue) -match 'GameBootService\.Profile' }

Write-Host "Archivos con acceso al perfil: $($files.Count)" -ForegroundColor Yellow
Write-Host ""

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    # Extraer nombre de clase
    if ($content -match 'class\s+(\w+)') {
        $className = $Matches[1]
    } else {
        $className = $file.BaseName
    }
    
    # Verificar patrones
    $subscribes = $content -match 'GameBootService\.OnProfileReady\s*\+='
    $registers = $content -match 'ProfileReadyDiagnostics\.RegisterSubscriber'
    $isStatic = $content -match 'static\s+class'
    
    if (-not $subscribes -and -not $isStatic) {
        $problemFiles += [PSCustomObject]@{
            Class = $className
            File = $file.Name
            Path = $file.FullName.Replace((Get-Location).Path + '\', '')
            Subscribes = $subscribes
            Registers = $registers
            IsStatic = $isStatic
        }
        
        Write-Host "PROBLEMA: $className" -ForegroundColor Red
        Write-Host "  Archivo: $($file.Name)" -ForegroundColor Gray
        Write-Host "  Se suscribe: $subscribes" -ForegroundColor $(if($subscribes){"Green"}else{"Red"})
        Write-Host "  Registra: $registers" -ForegroundColor $(if($registers){"Green"}else{"Red"})
        Write-Host ""
    } else {
        Write-Host "OK: $className" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESUMEN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total archivos analizados: $($files.Count)" -ForegroundColor White
Write-Host "Archivos con problemas: $($problemFiles.Count)" -ForegroundColor $(if($problemFiles.Count -gt 0){"Red"}else{"Green"})
Write-Host ""

if ($problemFiles.Count -gt 0) {
    Write-Host "SISTEMAS PROBLEMATICOS:" -ForegroundColor Red
    foreach ($p in $problemFiles) {
        Write-Host "  - $($p.Class)" -ForegroundColor Yellow
    }
    exit 1
} else {
    Write-Host "Todos los sistemas estan correctamente configurados!" -ForegroundColor Green
    exit 0
}
