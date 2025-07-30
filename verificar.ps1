# Script verificar_git.ps1
# Descripción Verifica si el repositorio Git existe. Si no, da opción para inicializar.

$proyecto = Get-Location
$gitDir = Join-Path $proyecto .git

if (Test-Path $gitDir) {
    Write-Host ✅ Ya estás en un repositorio Git. -ForegroundColor Green
    git status
} else {
    Write-Host ⚠️ No se detecta un repositorio Git en $proyecto -ForegroundColor Yellow
    
    $respuesta = Read-Host ¿Deseas inicializar uno ahora (sn)
    if ($respuesta -eq s) {
        git init
        Write-Host ✅ Repositorio Git inicializado.
        
        git add .
        git commit -m Primer commit automático
    } else {
        Write-Host Operación cancelada. -ForegroundColor Red
    }
}