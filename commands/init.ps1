#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Initializes the Unity Package project by replacing placeholders.

.DESCRIPTION
    Replaces placeholders in file content, filenames, and directory names.

    Placeholders:
    - YOUR_PACKAGE_ID                  UPM / reverse-domain identity (as provided)
    - YOUR_PACKAGE_ID_LOWERCASE        Lowercase form used in package.json and manifests
    - YOUR_ASSEMBLY_NAME               C# namespaces, asmdef names, and assembly filenames
    - YOUR_PACKAGE_NAME                Display / description name (not used for code identity)
    - YOUR_PACKAGE_NAME_INSTALLER
    - YOUR_PACKAGE_NAME_INSTALLER_FILE
    - YOUR_GITHUB_USERNAME_REPOSITORY

    Also renames the host Unity project folder `Unity-Package` to the GitHub
    repository name (the segment after `/` in -GitHubRepository), and rewrites
    path references accordingly. The string `Unity-Package-Template` is left alone
    so template attribution URLs stay intact.

    PackageId and AssemblyName are intentionally separate so a package like
    "com.company.cooltools" can use assemblies/namespaces such as "CoolTools".

.PARAMETER PackageId
    The UPM package ID (e.g. "com.company.package"). Used for package.json, local
    test manifests, OpenUPM/release tarball naming, and Installer.PackageId.

.PARAMETER AssemblyName
    Root name for assemblies and C# namespaces (e.g. "CoolTools" or "Company.CoolTools").
    Becomes CoolTools.Runtime, CoolTools.Editor, CoolTools.Installer, etc.
    Must be a valid dotted C# identifier (no spaces).

.PARAMETER PackageName
    Human-readable package display name (e.g. "Cool Package"). Used for installer
    folder naming and other descriptive text - not for asmdefs or namespaces.

.PARAMETER InstallerExtraPath
    Optional. Extra path segment between Assets/ and the Installer folder.
    For example, "Editor" places the installer at Assets/Editor/{Package} Installer.
    If omitted, you will be prompted (press Enter to skip).

.PARAMETER GitHubRepository
    GitHub "Username/Repository-Name" (e.g. "MyGitHubUsername/My-Repository-Name").
    The repository name becomes the Unity project folder (replacing `Unity-Package`).

.EXAMPLE
    .\init.ps1 -PackageId "com.mycompany.coolpackage" -AssemblyName "CoolPackage" -PackageName "Cool Package" -GitHubRepository "myusername/cool-package"

.EXAMPLE
    .\init.ps1 -PackageId "com.mycompany.coolpackage" -AssemblyName "MyCompany.CoolPackage" -PackageName "Cool Package" -InstallerExtraPath "Editor" -GitHubRepository "myusername/cool-package"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$PackageId,

    [Parameter(Mandatory = $true)]
    [string]$AssemblyName,

    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [Parameter(Mandatory = $false)]
    [string]$InstallerExtraPath,

    [Parameter(Mandatory = $true)]
    [string]$GitHubRepository
)

$ErrorActionPreference = "Stop"

function Test-IsDottedIdentifier {
    param([string]$Value)
    return $Value -match '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$'
}

function Test-ShouldIgnore {
    param(
        [string]$ItemPath,
        [string]$BasePath,
        [string[]]$IgnorePatterns
    )

    if (-not $IgnorePatterns -or $IgnorePatterns.Count -eq 0) {
        return $false
    }

    $RelativePath = $ItemPath.Substring($BasePath.Length).TrimStart('\', '/')
    $RelativePath = $RelativePath -replace '\\', '/'

    foreach ($Pattern in $IgnorePatterns) {
        $RegexPattern = "^" + ($Pattern -replace '\*', '[^/]+') + "(/.*)?$"
        if ($RelativePath -match $RegexPattern) {
            return $true
        }
    }

    return $false
}

function Get-TargetPathInfo {
    param($Entry)

    if ($Entry -is [string]) {
        return @{ Path = $Entry; Ignore = @() }
    }
    elseif ($Entry -is [hashtable]) {
        return @{
            Path   = $Entry.Path
            Ignore = if ($Entry.Ignore) { $Entry.Ignore } else { @() }
        }
    }

    throw "Invalid target path entry: $Entry"
}

function Get-TargetFiles {
    param(
        [string]$RepoRoot,
        [array]$TargetPaths
    )

    $Files = @()
    foreach ($Entry in $TargetPaths) {
        $TargetInfo = Get-TargetPathInfo $Entry
        $FullPath = Join-Path $RepoRoot $TargetInfo.Path
        if (-not (Test-Path $FullPath)) {
            Write-Warning "Path not found: $FullPath"
            continue
        }

        if ((Get-Item $FullPath).PSIsContainer) {
            foreach ($File in (Get-ChildItem -Path $FullPath -Recurse -File)) {
                if (-not (Test-ShouldIgnore -ItemPath $File.FullName -BasePath $FullPath -IgnorePatterns $TargetInfo.Ignore)) {
                    $Files += $File
                }
            }
        }
        else {
            $Files += Get-Item $FullPath
        }
    }

    return $Files
}

function Get-TargetItems {
    param(
        [string]$RepoRoot,
        [array]$TargetPaths
    )

    $Items = @()
    foreach ($Entry in $TargetPaths) {
        $TargetInfo = Get-TargetPathInfo $Entry
        $FullPath = Join-Path $RepoRoot $TargetInfo.Path
        if (-not (Test-Path $FullPath)) {
            continue
        }

        if ((Get-Item $FullPath).PSIsContainer) {
            foreach ($Item in (Get-ChildItem -Path $FullPath -Recurse)) {
                if (-not (Test-ShouldIgnore -ItemPath $Item.FullName -BasePath $FullPath -IgnorePatterns $TargetInfo.Ignore)) {
                    $Items += $Item
                }
            }
        }
        else {
            $Items += Get-Item $FullPath
        }
    }

    return $Items | Sort-Object -Property FullName -Descending | Select-Object -Unique
}

function Invoke-PlaceholderReplace {
    param(
        [string]$Text,
        [hashtable]$Map,
        [string[]]$SortedKeys
    )

    $Result = $Text
    foreach ($Key in $SortedKeys) {
        if ($Result.Contains($Key)) {
            $Result = $Result.Replace($Key, [string]$Map[$Key])
        }
    }
    return $Result
}

# Reads a text file, detecting its encoding from the BOM (UTF-8 / UTF-16 LE / UTF-16 BE; no BOM = UTF-8).
# Get-Content/Set-Content without -Encoding are not used on purpose: Windows PowerShell 5.1 decodes and
# writes BOM-less files with the ANSI code page (and drops BOMs), PowerShell 7 drops BOMs on write.
# Returns $null for files that are not valid text (NUL bytes or invalid UTF-8), e.g. images or DLLs.
function Read-TextFile {
    param([string]$Path)

    $Bytes = [System.IO.File]::ReadAllBytes($Path)
    $PreambleLength = 0

    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
        $Encoding = New-Object System.Text.UTF8Encoding($false, $true)
        $PreambleLength = 3
    }
    elseif ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) {
        $Encoding = New-Object System.Text.UnicodeEncoding($false, $false, $true)
        $PreambleLength = 2
    }
    elseif ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) {
        $Encoding = New-Object System.Text.UnicodeEncoding($true, $false, $true)
        $PreambleLength = 2
    }
    else {
        if ([Array]::IndexOf($Bytes, [byte]0) -ge 0) {
            return $null
        }
        $Encoding = New-Object System.Text.UTF8Encoding($false, $true)
    }

    try {
        $Text = $Encoding.GetString($Bytes, $PreambleLength, $Bytes.Length - $PreambleLength)
    }
    catch {
        # Invalid byte sequence for the detected encoding (PowerShell wraps the DecoderFallbackException).
        return $null
    }

    $Preamble = New-Object byte[] $PreambleLength
    [Array]::Copy($Bytes, $Preamble, $PreambleLength)

    return @{
        Text     = $Text
        Encoding = $Encoding
        Preamble = $Preamble
    }
}

# Writes text back with the encoding and BOM it was read with (line endings are kept as they are in the text).
function Write-TextFile {
    param(
        [string]$Path,
        [string]$Text,
        [System.Text.Encoding]$Encoding,
        [byte[]]$Preamble
    )

    $Body = $Encoding.GetBytes($Text)
    $Stream = New-Object System.IO.MemoryStream
    try {
        $Stream.Write($Preamble, 0, $Preamble.Length)
        $Stream.Write($Body, 0, $Body.Length)
        [System.IO.File]::WriteAllBytes($Path, $Stream.ToArray())
    }
    finally {
        $Stream.Dispose()
    }
}

function Get-UnityProjectNameFromGitHubRepository {
    param([string]$Repository)

    if ($Repository -notmatch '^[^/\s]+/([^/\s]+)$') {
        throw "GitHubRepository must be 'Owner/Repository-Name', got: '$Repository'"
    }

    $Name = $Matches[1]
    if ($Name -match '[<>:"/\\|?*]') {
        throw "GitHub repository name contains invalid path characters: '$Name'"
    }
    if ($Name -eq '.' -or $Name -eq '..') {
        throw "GitHub repository name is not a valid folder name: '$Name'"
    }

    return $Name
}

# Rewrites path references to the host Unity project folder without touching
# "Unity-Package-Template" (template repo name / attribution URLs).
function Invoke-UnityProjectPathReplace {
    param(
        [string]$Text,
        [string]$UnityProjectName
    )

    # Protect $ in the replacement so regex does not treat it as a group reference.
    $SafeReplacement = $UnityProjectName.Replace('$', '$$')
    return [regex]::Replace($Text, 'Unity-Package(?!-Template)', $SafeReplacement)
}

# --- Prompt / normalize inputs ------------------------------------------------

if (-not $PSBoundParameters.ContainsKey('InstallerExtraPath')) {
    Write-Host "Optional: Enter an extra path for the Installer (e.g., 'Editor')." -ForegroundColor Cyan
    Write-Host "Press Enter to skip." -ForegroundColor Gray
    $InstallerExtraPath = Read-Host "Installer Extra Path"
}

$PackageId = $PackageId.Trim()
$AssemblyName = $AssemblyName.Trim()
$PackageName = $PackageName.Trim()
$GitHubRepository = $GitHubRepository.Trim()

if ([string]::IsNullOrWhiteSpace($PackageId)) {
    throw "PackageId cannot be empty."
}
if ([string]::IsNullOrWhiteSpace($AssemblyName)) {
    throw "AssemblyName cannot be empty."
}
if ([string]::IsNullOrWhiteSpace($GitHubRepository)) {
    throw "GitHubRepository cannot be empty."
}
if (-not (Test-IsDottedIdentifier $AssemblyName)) {
    throw "AssemblyName must be a dotted C# identifier (e.g. 'CoolTools' or 'Company.CoolTools'), got: '$AssemblyName'"
}
if ($AssemblyName -eq $PackageId) {
    Write-Host "Warning: AssemblyName equals PackageId. They can differ (recommended: PascalCase assembly root)." -ForegroundColor DarkYellow
}

if (-not [string]::IsNullOrWhiteSpace($InstallerExtraPath)) {
    $InstallerExtraPath = ($InstallerExtraPath.Trim() -replace '\\', '/').TrimStart('/').TrimEnd('/')
}

$UnityProjectFolderSource = "Unity-Package"
$UnityProjectName = Get-UnityProjectNameFromGitHubRepository -Repository $GitHubRepository

$PackageIdLowercase = $PackageId.ToLowerInvariant()
$PackageNameInstallerBase = "$PackageName Installer"
$PackageNameInstallerFile = $PackageNameInstallerBase -replace ' ', '-'

if ([string]::IsNullOrWhiteSpace($InstallerExtraPath)) {
    $PackageNameInstaller = $PackageNameInstallerBase
}
else {
    $PackageNameInstaller = "$InstallerExtraPath/$PackageNameInstallerBase"
}

# Content replacements: installer path may include InstallerExtraPath.
# Rename map: folder name stays the base installer name (path created separately).
$Replacements = [ordered]@{
    "YOUR_PACKAGE_ID_LOWERCASE"        = $PackageIdLowercase
    "YOUR_PACKAGE_NAME_INSTALLER_FILE" = $PackageNameInstallerFile
    "YOUR_PACKAGE_NAME_INSTALLER"      = $PackageNameInstaller
    "YOUR_GITHUB_USERNAME_REPOSITORY"  = $GitHubRepository
    "YOUR_ASSEMBLY_NAME"               = $AssemblyName
    "YOUR_PACKAGE_NAME"                = $PackageName
    "YOUR_PACKAGE_ID"                  = $PackageId
}

$ReplacementsForRenaming = [ordered]@{
    "YOUR_PACKAGE_ID_LOWERCASE"        = $PackageIdLowercase
    "YOUR_PACKAGE_NAME_INSTALLER_FILE" = $PackageNameInstallerFile
    "YOUR_PACKAGE_NAME_INSTALLER"      = $PackageNameInstallerBase
    "YOUR_GITHUB_USERNAME_REPOSITORY"  = $GitHubRepository
    "YOUR_ASSEMBLY_NAME"               = $AssemblyName
    "YOUR_PACKAGE_NAME"                = $PackageName
    "YOUR_PACKAGE_ID"                  = $PackageId
}

# Longest-first to avoid partial overlaps (e.g. YOUR_PACKAGE_ID inside YOUR_PACKAGE_ID_LOWERCASE)
$SortedKeys = @($Replacements.Keys | Sort-Object { $_.Length } -Descending)

$ScriptPath = $MyInvocation.MyCommand.Path
$ScriptDir = Split-Path -Parent $ScriptPath
$RepoRoot = Split-Path -Parent $ScriptDir

Write-Host "Initializing package with:" -ForegroundColor Cyan
Write-Host "  Package ID:          $PackageId"
Write-Host "  Package ID (lower):  $PackageIdLowercase"
Write-Host "  Assembly / namespace: $AssemblyName"
Write-Host "  Display name:        $PackageName"
Write-Host "  Unity project folder: $UnityProjectFolderSource -> $UnityProjectName"
Write-Host "  Installer:           $PackageNameInstaller"
Write-Host "  Installer file:      $PackageNameInstallerFile"
Write-Host "  GitHub repository:   $GitHubRepository"
if (-not [string]::IsNullOrWhiteSpace($InstallerExtraPath)) {
    Write-Host "  Installer extra path: $InstallerExtraPath"
}
Write-Host ""

$TargetPaths = @(
    @{ Path = "commands" },
    @{
        Path   = "Installer"
        Ignore = @("/Library", "/Temp", "/Logs", "/obj")
    },
    @{
        Path   = $UnityProjectFolderSource
        Ignore = @("/Library", "/Temp", "/Logs", "/obj")
    },
    @{
        Path   = "Unity-Tests"
        Ignore = @("*/Library", "*/Temp", "*/Logs", "*/obj")
    },
    @{ Path = "docs" },
    @{ Path = "README.md" },
    @{ Path = ".github" }
)

$UnityProjectSourcePath = Join-Path $RepoRoot $UnityProjectFolderSource
if (-not (Test-Path -LiteralPath $UnityProjectSourcePath)) {
    throw "Unity project folder not found: $UnityProjectSourcePath"
}
if ($UnityProjectName -ne $UnityProjectFolderSource) {
    $UnityProjectDestPath = Join-Path $RepoRoot $UnityProjectName
    if (Test-Path -LiteralPath $UnityProjectDestPath) {
        throw "Cannot rename '$UnityProjectFolderSource' to '$UnityProjectName' - destination already exists."
    }
}

# --- 1. Replace content -------------------------------------------------------

Write-Host "Replacing content in files..." -ForegroundColor Yellow
foreach ($File in (Get-TargetFiles -RepoRoot $RepoRoot -TargetPaths $TargetPaths)) {
    # The placeholders in this script are its own configuration; rewriting them would leave an
    # unparseable script (e.g. duplicate hashtable keys when PackageId is already lowercase).
    if ([string]::Equals($File.FullName, $ScriptPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    $TextFile = Read-TextFile -Path $File.FullName
    if ($null -eq $TextFile) {
        Write-Host "  Skipped (not text): $($File.FullName)" -ForegroundColor DarkGray
        continue
    }

    $Content = $TextFile.Text
    $NewContent = Invoke-PlaceholderReplace -Text $Content -Map $Replacements -SortedKeys $SortedKeys
    $NewContent = Invoke-UnityProjectPathReplace -Text $NewContent -UnityProjectName $UnityProjectName
    if ($NewContent -ne $Content) {
        $Preamble = $TextFile.Preamble
        if ($Preamble.Length -eq 0 -and $TextFile.Encoding -is [System.Text.UTF8Encoding] -and
            $File.Extension -in @('.ps1', '.psm1', '.psd1') -and $NewContent -match '[^\x00-\x7F]') {
            # Windows PowerShell 5.1 parses BOM-less scripts with the ANSI code page, so a non-ASCII value
            # (e.g. a package name used in paths) would be misread. A UTF-8 BOM makes 5.1 read it as UTF-8.
            $Preamble = (New-Object System.Text.UTF8Encoding($true)).GetPreamble()
        }
        Write-TextFile -Path $File.FullName -Text $NewContent -Encoding $TextFile.Encoding -Preamble $Preamble
        Write-Host "  Updated: $($File.FullName)" -ForegroundColor Gray
    }
}

# --- 1.5. Optional installer extra path ---------------------------------------

if (-not [string]::IsNullOrWhiteSpace($InstallerExtraPath)) {
    Write-Host "Creating extra path and moving installer folder..." -ForegroundColor Yellow
    $InstallerAssetsDir = Join-Path $RepoRoot "Installer/Assets"
    $SourceFolder = Join-Path $InstallerAssetsDir "YOUR_PACKAGE_NAME_INSTALLER"
    $ExtraPathDir = Join-Path $InstallerAssetsDir $InstallerExtraPath

    if (-not (Test-Path $ExtraPathDir)) {
        New-Item -Path $ExtraPathDir -ItemType Directory -Force | Out-Null
        Write-Host "  Created: $ExtraPathDir" -ForegroundColor Gray
    }

    if (Test-Path $SourceFolder) {
        $DestFolder = Join-Path $ExtraPathDir "YOUR_PACKAGE_NAME_INSTALLER"
        Move-Item -Path $SourceFolder -Destination $DestFolder -Force
        Write-Host "  Moved installer to: $InstallerExtraPath/" -ForegroundColor Gray
    }
}

# --- 2. Rename files and directories (depth-first) -----------------------------

Write-Host "Renaming files and directories..." -ForegroundColor Yellow
foreach ($Item in (Get-TargetItems -RepoRoot $RepoRoot -TargetPaths $TargetPaths)) {
    $NewName = Invoke-PlaceholderReplace -Text $Item.Name -Map $ReplacementsForRenaming -SortedKeys $SortedKeys
    if ($NewName -ne $Item.Name) {
        Rename-Item -Path $Item.FullName -NewName $NewName
        Write-Host "  Renamed: $($Item.Name) -> $NewName" -ForegroundColor Gray
    }
}

# --- 3. Rename host Unity project folder to match GitHub repository name ------

if ($UnityProjectName -ne $UnityProjectFolderSource) {
    Write-Host "Renaming Unity project folder..." -ForegroundColor Yellow
    Rename-Item -LiteralPath $UnityProjectSourcePath -NewName $UnityProjectName
    Write-Host "  Renamed: $UnityProjectFolderSource -> $UnityProjectName" -ForegroundColor Gray
}

Write-Host "Done!" -ForegroundColor Green
Write-Host ""
Write-Host "Next: open Unity projects to generate .meta files, then fill package.json author/description." -ForegroundColor Cyan
