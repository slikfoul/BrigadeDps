param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$DllPath = (Join-Path $PSScriptRoot 'src\bin\Release\net6.0\BrigadeDps.dll')
)
$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'packaging\manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$version = $manifest.version_number
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\BrigadeDps.csproj') -Raw
if ($project.Project.PropertyGroup.Version -ne $version) { throw 'Project and package versions must match.' }
if (!(Test-Path -LiteralPath $DllPath -PathType Leaf)) { throw 'Build the Release configuration before packaging.' }
$dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($DllPath).FileVersion
if ($dllVersion -ne "$version.0") { throw "DLL version $dllVersion does not match package $version." }
$packageName = "slikfoul-BrigadeDps-$version"
$packageRoot = Join-Path $OutputDirectory $packageName
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $DllPath -Destination (Join-Path $packageRoot 'BrigadeDps.dll') -Force
foreach ($file in @('README.md', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $packageRoot -Force
}
Copy-Item -LiteralPath $manifestPath, (Join-Path $PSScriptRoot 'packaging\icon.png') -Destination $packageRoot -Force
$expected = @('BrigadeDps.dll', 'CHANGELOG.md', 'README.md', 'icon.png', 'manifest.json') | Sort-Object
$archivePath = Join-Path $OutputDirectory "$packageName.zip"
$packageFiles = @($expected | ForEach-Object { Join-Path $packageRoot $_ })
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archivePath -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $actual = @($archive.Entries.FullName | Sort-Object)
    if (Compare-Object $expected $actual) { throw 'Unexpected package contents.' }
} finally { $archive.Dispose() }
Get-Item -LiteralPath $archivePath | Select-Object FullName, Length
