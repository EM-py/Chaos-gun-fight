$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/warn:4', ('/out:' + (Join-Path $taskRoot 'StickMayhem.exe')), ('/reference:' + (Join-Path $wpf 'PresentationFramework.dll')), ('/reference:' + (Join-Path $wpf 'PresentationCore.dll')), ('/reference:' + (Join-Path $wpf 'WindowsBase.dll')), '/reference:System.Xaml.dll', '/reference:System.Web.Extensions.dll')
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output 'Built StickMayhem.exe'
