[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'arm64')]
    [string[]]$Architectures = @('x64', 'arm64'),

    [string]$OutputRoot = 'artifacts\release',

    [switch]$SkipSigning
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'LinkSearch\LinkSearch.csproj'
$manifestPath = Join-Path $repoRoot 'LinkSearch\Package.appxmanifest'
$outputRootPath = Join-Path $repoRoot $OutputRoot
$packagesRoot = Join-Path $outputRootPath 'packages'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Get-PackageIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    [xml]$manifest = Get-Content -LiteralPath $Path -Raw
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $namespaceManager.AddNamespace('pkg', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')

    $identity = $manifest.SelectSingleNode('/pkg:Package/pkg:Identity', $namespaceManager)
    if (-not $identity) {
        throw 'Package.appxmanifest is missing /Package/Identity.'
    }

    if ([string]::IsNullOrWhiteSpace($identity.Version)) {
        throw 'Package.appxmanifest Identity is missing Version.'
    }

    if ([string]::IsNullOrWhiteSpace($identity.Publisher)) {
        throw 'Package.appxmanifest Identity is missing Publisher.'
    }

    return @{
        Version = $identity.Version
        Publisher = $identity.Publisher
    }
}

function Get-SignToolPath {
    $kitsRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        throw 'Windows SDK signtool.exe was not found. Install the Windows SDK or use -SkipSigning.'
    }

    $tool = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter 'signtool.exe' |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        throw 'Windows SDK x64 signtool.exe was not found. Install the Windows SDK or use -SkipSigning.'
    }

    return $tool.FullName
}

function New-TestSigningCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Publisher,

        [Parameter(Mandatory = $true)]
        [string]$CertificatePath
    )

    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Publisher `
        -KeyUsage DigitalSignature `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(3)

    Export-Certificate -Cert $certificate -FilePath $CertificatePath | Out-Null
    return $certificate
}

function Get-ArchitectureSettings {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Architecture
    )

    switch ($Architecture) {
        'x64' {
            return @{
                Platform = 'x64'
                RuntimeIdentifier = 'win-x64'
                PackageSuffix = 'x64'
            }
        }
        'arm64' {
            return @{
                Platform = 'ARM64'
                RuntimeIdentifier = 'win-arm64'
                PackageSuffix = 'arm64'
            }
        }
    }
}

$packageIdentity = Get-PackageIdentity -Path $manifestPath
$packageVersion = $packageIdentity.Version
$publisher = $packageIdentity.Publisher
$releaseVersion = $packageVersion -replace '\.0$', ''

if (Test-Path -LiteralPath $outputRootPath) {
    Remove-Item -LiteralPath $outputRootPath -Recurse -Force
}

New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null

$signToolPath = $null
$certificate = $null
$certificatePath = Join-Path $packagesRoot 'LinkSearch_TestCertificate.cer'

try {
    if (-not $SkipSigning) {
        $signToolPath = Get-SignToolPath
        $certificate = New-TestSigningCertificate -Publisher $publisher -CertificatePath $certificatePath
    }

    foreach ($architecture in $Architectures) {
        $settings = Get-ArchitectureSettings -Architecture $architecture
        $platform = $settings.Platform
        $runtimeIdentifier = $settings.RuntimeIdentifier
        $packageSuffix = $settings.PackageSuffix
        $targetFramework = 'net9.0-windows10.0.22621.0'
        $appPackagesRoot = Join-Path $repoRoot "LinkSearch\bin\$platform\$Configuration\$targetFramework\$runtimeIdentifier\AppPackages"

        if (Test-Path -LiteralPath $appPackagesRoot) {
            Remove-Item -LiteralPath $appPackagesRoot -Recurse -Force
        }

        Invoke-CheckedCommand -FilePath 'dotnet' -Arguments @(
            'msbuild',
            $projectPath,
            '/restore',
            '/t:Publish',
            "/p:Configuration=$Configuration",
            "/p:Platform=$platform",
            "/p:RuntimeIdentifier=$runtimeIdentifier",
            '/p:PublishProfile=',
            '/p:SelfContained=true',
            '/p:PublishSingleFile=false',
            '/p:PublishReadyToRun=true',
            '/p:GenerateAppxPackageOnBuild=true',
            '/p:UapAppxPackageBuildMode=SideloadOnly',
            '/p:AppxBundle=Never'
        )

        $msix = Get-ChildItem -LiteralPath $appPackagesRoot -Recurse -Filter '*.msix' |
            Where-Object { $_.Name -like "*_$packageSuffix.msix" } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1

        if (-not $msix) {
            throw "MSIX package for $architecture was not generated under $appPackagesRoot."
        }

        $destination = Join-Path $packagesRoot $msix.Directory.Name
        Copy-Item -LiteralPath $msix.Directory.FullName -Destination $destination -Recurse -Force

        if (-not $SkipSigning) {
            Copy-Item -LiteralPath $certificatePath -Destination (Join-Path $destination 'LinkSearch_TestCertificate.cer') -Force

            $packageToSign = Get-ChildItem -LiteralPath $destination -Filter '*.msix' | Select-Object -First 1
            Invoke-CheckedCommand -FilePath $signToolPath -Arguments @(
                'sign',
                '/fd', 'SHA256',
                '/sha1', $certificate.Thumbprint,
                $packageToSign.FullName
            )
        }
    }
}
finally {
    if ($certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}

$readmePath = Join-Path $packagesRoot 'README-install.txt'
Set-Content -LiteralPath $readmePath -Encoding UTF8 -Value @"
LinkSearch $packageVersion MSIX sideload packages

Recommended install: https://apps.microsoft.com/detail/9MZ9Q4CFP2N9

These packages are provided as a manual sideload fallback for users who cannot install from Microsoft Store. They are signed with a temporary self-signed certificate generated at package time.

Install:
1. Extract the release zip.
2. Open the folder matching your CPU architecture, such as LinkSearch_${packageVersion}_x64_Test.
3. Right-click Add-AppDevPackage.ps1 and choose Run with PowerShell.
4. Allow the script to install LinkSearch_TestCertificate.cer when prompted.
5. Open PowerToys Command Palette and enable the LinkSearch extension.

Requirement: Microsoft PowerToys with Command Palette support.
"@

$zipPath = Join-Path $outputRootPath "LinkSearch_${releaseVersion}_msix_sideload_packages.zip"
Compress-Archive -Path (Join-Path $packagesRoot '*') -DestinationPath $zipPath -Force

$hashPath = "$zipPath.sha256"
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
Set-Content -LiteralPath $hashPath -Encoding ASCII -Value "$($hash.Hash)  $(Split-Path -Leaf $zipPath)"

Write-Host "Created release package: $zipPath"
Write-Host "Created checksum: $hashPath"
