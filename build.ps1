#Requires -Version 5.1
<#
    build.ps1 - Compila DTXManiaNX y opcionalmente genera el instalador.

    Uso:
        .\build.ps1                         # Release|x86 + instalador
        .\build.ps1 -Platform x64          # Release|x64 + instalador
        .\build.ps1 -Configuration Debug   # Debug|x86 + instalador
        .\build.ps1 -SkipInstaller         # solo compilar (sin Inno Setup)

    Requisitos:
      * MSBuild (Visual Studio 2019/2022 o "Build Tools for Visual Studio")
        con la carga de trabajo ".NET desktop build tools".
      * .NET Framework 4.7.1 Developer Pack (targeting pack).
      * Inno Setup 6 (solo si no se usa -SkipInstaller).
#>
[CmdletBinding()]
param(
    [ValidateSet( 'Debug', 'Release' )]
    [string] $Configuration = 'Release',

    [ValidateSet( 'x86', 'x64' )]
    [string] $Platform = 'x86',

    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root     = Split-Path -Parent $MyInvocation.MyCommand.Definition
$solution = Join-Path $root 'DTXMania.sln'
$iss      = Join-Path $root 'Installer setup.iss'

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if( Test-Path -LiteralPath $vswhere ) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null
        if( $found ) { return @($found)[0] }
    }
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe')
    )
    foreach( $c in $candidates ) {
        if( Test-Path -LiteralPath $c ) { return $c }
    }
    $cmd = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if( $cmd ) { return $cmd.Source }
    return $null
}

function Find-ISCC {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 5\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach( $c in $candidates ) {
        if( Test-Path -LiteralPath $c ) { return $c }
    }
    foreach( $hive in @( 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
                         'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
                         'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1' ) ) {
        try {
            $loc = (Get-ItemProperty -Path $hive -ErrorAction Stop).InstallLocation
            if( $loc ) {
                $exe = Join-Path $loc 'ISCC.exe'
                if( Test-Path -LiteralPath $exe ) { return $exe }
            }
        } catch { }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if( $cmd ) { return $cmd.Source }
    return $null
}

Write-Host "== DTXManiaNX build ==" -ForegroundColor Cyan
Write-Host "Solucion : $solution"
Write-Host "Config   : $Configuration|$Platform"
Write-Host ""

if( -not (Test-Path -LiteralPath $solution) ) {
    throw "No se encontro la solucion: $solution"
}

$msbuild = Find-MSBuild
if( -not $msbuild ) {
    throw @"
No se encontro MSBuild.
Instala 'Build Tools for Visual Studio 2022' (o Visual Studio) con la carga
'.NET desktop build tools' y el '.NET Framework 4.7.1 Developer Pack':
  https://visualstudio.microsoft.com/downloads/
"@
}
Write-Host "MSBuild  : $msbuild" -ForegroundColor DarkGray

& $msbuild $solution /nologo /m `
    /t:Build `
    "/p:Configuration=$Configuration" `
    "/p:Platform=$Platform" `
    /v:minimal
if( $LASTEXITCODE -ne 0 ) {
    throw "La compilacion fallo (codigo $LASTEXITCODE)."
}
Write-Host "Compilacion OK -> Runtime\" -ForegroundColor Green

if( $SkipInstaller ) {
    Write-Host "Instalador omitido (-SkipInstaller)." -ForegroundColor Yellow
    exit 0
}

$iscc = Find-ISCC
if( -not $iscc ) {
    throw @"
No se encontro Inno Setup (ISCC.exe).
Instala Inno Setup 6:
  https://jrsoftware.org/isinfo.php
"@
}
Write-Host "ISCC     : $iscc" -ForegroundColor DarkGray

& $iscc $iss
if( $LASTEXITCODE -ne 0 ) {
    throw "Inno Setup fallo (codigo $LASTEXITCODE)."
}

$installers = New-Object System.Collections.Generic.List[object]
$installers.AddRange( @(Get-ChildItem -LiteralPath $root -Filter 'DTXManiaNX-*.exe' -ErrorAction SilentlyContinue) )
$installers.AddRange( @(Get-ChildItem -LiteralPath (Join-Path $root 'Output') -Filter 'DTXManiaNX-*.exe' -ErrorAction SilentlyContinue) )
$installer = $installers | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
if( $installer ) {
    Write-Host "Listo: $($installer.FullName)" -ForegroundColor Green
} else {
    Write-Host "Listo: instalador generado en $root" -ForegroundColor Green
}
