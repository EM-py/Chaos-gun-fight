$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/warn:4', ('/out:' + (Join-Path $taskRoot 'StickMayhem.exe')), ('/reference:' + (Join-Path $wpf 'PresentationFramework.dll')), ('/reference:' + (Join-Path $wpf 'PresentationCore.dll')), ('/reference:' + (Join-Path $wpf 'WindowsBase.dll')), '/reference:System.Xaml.dll', '/reference:System.Web.Extensions.dll')
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output 'Built StickMayhem.exe'
$updater = Join-Path $taskRoot 'updater\Program.cs'
$updaterArguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/warn:4', ('/out:' + (Join-Path $taskRoot 'StickMayhem.Update.exe')), ('/reference:' + (Join-Path $wpf 'PresentationFramework.dll')), ('/reference:' + (Join-Path $wpf 'PresentationCore.dll')), ('/reference:' + (Join-Path $wpf 'WindowsBase.dll')), '/reference:System.Xaml.dll', ('/reference:' + (Join-Path $framework 'System.IO.Compression.dll')), ('/reference:' + (Join-Path $framework 'System.IO.Compression.FileSystem.dll')), $updater)
& (Join-Path $framework 'csc.exe') @updaterArguments
if ($LASTEXITCODE -ne 0) { throw 'Updater build failed' }
Write-Output 'Built StickMayhem.Update.exe'
$versionLine = Select-String -LiteralPath (Join-Path $taskRoot 'src\Game.cs') -Pattern 'Version = "StickMayhem/([^\"]+)"' | Select-Object -First 1
if ($null -eq $versionLine -or $versionLine.Matches.Count -eq 0) { throw 'Could not read game version from src\Game.cs' }
$version = $versionLine.Matches[0].Groups[1].Value
$packagePath = Join-Path $taskRoot 'StickMayhem-share.zip'
$packageFiles = @('StickMayhem.exe', 'StickMayhem.Update.exe', 'README.md', '使用说明.txt', 'CHANGELOG.md') | ForEach-Object { Join-Path $taskRoot $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath $packagePath -Force
$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    Version = $version
    PackageUrl = 'https://raw.githubusercontent.com/EM-py/Chaos-gun-fight/main/StickMayhem-share.zip'
    Sha256 = $packageHash
    Notes = '突击步枪与冲锋枪改为三十发后切回手枪；地图道具限时闪烁消失；新增枪械后坐力等级。'
} | ConvertTo-Json -Compress
[System.IO.File]::WriteAllText((Join-Path $taskRoot 'update.json'), $manifest, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ('Packaged v' + $version + ' · SHA-256 ' + $packageHash)
