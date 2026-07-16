param(
    [Parameter(Mandatory = $false)]
    [string]$RootPath = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$RootPath = (Resolve-Path $RootPath).Path
$TimeStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$OutputDir = Join-Path $RootPath "_DeskPulseFolderScan_$TimeStamp"
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "Scanning: $RootPath"
Write-Host "Output:   $OutputDir"

# Avoid scanning the output directory itself.
$allItems = Get-ChildItem -LiteralPath $RootPath -Force -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike "$OutputDir*" }

$files = $allItems | Where-Object { -not $_.PSIsContainer }
$folders = $allItems | Where-Object { $_.PSIsContainer }

# Complete file inventory.
$fileInventory = foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($RootPath.Length).TrimStart('\')
    [PSCustomObject]@{
        RelativePath     = $relativePath
        Extension        = $file.Extension
        SizeBytes        = $file.Length
        SizeMB           = [math]::Round($file.Length / 1MB, 3)
        LastWriteTime    = $file.LastWriteTime
        Attributes       = $file.Attributes.ToString()
    }
}
$fileInventory |
    Sort-Object RelativePath |
    Export-Csv -LiteralPath (Join-Path $OutputDir "FileInventory.csv") -NoTypeInformation -Encoding UTF8

# Largest files.
$fileInventory |
    Sort-Object SizeBytes -Descending |
    Select-Object -First 200 |
    Export-Csv -LiteralPath (Join-Path $OutputDir "LargestFiles.csv") -NoTypeInformation -Encoding UTF8

# Folder sizes.
$folderInventory = foreach ($folder in @($RootPath) + $folders.FullName) {
    $folderFiles = $files | Where-Object {
        $_.FullName.StartsWith($folder, [System.StringComparison]::OrdinalIgnoreCase)
    }
    $size = ($folderFiles | Measure-Object Length -Sum).Sum
    if ($null -eq $size) { $size = 0 }

    $relativePath = if ($folder -eq $RootPath) {
        "."
    } else {
        $folder.Substring($RootPath.Length).TrimStart('\')
    }

    [PSCustomObject]@{
        RelativePath = $relativePath
        FileCount    = @($folderFiles).Count
        SizeBytes    = [int64]$size
        SizeMB       = [math]::Round($size / 1MB, 3)
    }
}
$folderInventory |
    Sort-Object SizeBytes -Descending |
    Export-Csv -LiteralPath (Join-Path $OutputDir "FolderSizes.csv") -NoTypeInformation -Encoding UTF8

# Likely generated or disposable folders. This is only a report; nothing is deleted.
$generatedFolderNames = @(
    "bin", "obj", ".vs", "publish", "artifacts", "TestResults",
    "packages", "Debug", "Release", "x64", "x86"
)

$generatedFolders = $folders |
    Where-Object { $generatedFolderNames -contains $_.Name } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($RootPath.Length).TrimStart('\')
        $size = ($files | Where-Object {
            $_.FullName.StartsWith($_.FullName, [System.StringComparison]::OrdinalIgnoreCase)
        } | Measure-Object Length -Sum).Sum
        [PSCustomObject]@{
            RelativePath = $relativePath
            FolderName   = $_.Name
            LastWriteTime = $_.LastWriteTime
        }
    }

$generatedFolders |
    Sort-Object RelativePath |
    Export-Csv -LiteralPath (Join-Path $OutputDir "GeneratedFolderCandidates.csv") -NoTypeInformation -Encoding UTF8

# Installer and archive files.
$releaseCandidates = $files |
    Where-Object {
        $_.Extension -in @(".exe", ".msi", ".zip", ".7z", ".rar") -or
        $_.Name -match "(?i)setup|installer|handover|release"
    } |
    ForEach-Object {
        [PSCustomObject]@{
            RelativePath  = $_.FullName.Substring($RootPath.Length).TrimStart('\')
            SizeMB        = [math]::Round($_.Length / 1MB, 3)
            LastWriteTime = $_.LastWriteTime
        }
    }

$releaseCandidates |
    Sort-Object LastWriteTime -Descending |
    Export-Csv -LiteralPath (Join-Path $OutputDir "ReleaseAndArchiveCandidates.csv") -NoTypeInformation -Encoding UTF8

# Simple text tree, limited to paths rather than graphical indentation.
$allItems |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($RootPath.Length).TrimStart('\')
        if ($_.PSIsContainer) { "[DIR]  $relativePath" } else { "[FILE] $relativePath" }
    } |
    Set-Content -LiteralPath (Join-Path $OutputDir "FolderTree.txt") -Encoding UTF8

# Git information, when this is a Git working tree.
$gitAvailable = Get-Command git -ErrorAction SilentlyContinue
if ($gitAvailable) {
    Push-Location $RootPath
    try {
        $insideRepo = git rev-parse --is-inside-work-tree 2>$null
        if ($insideRepo -eq "true") {
            git status --short --ignored |
                Set-Content -LiteralPath (Join-Path $OutputDir "GitStatusIncludingIgnored.txt") -Encoding UTF8

            git ls-files |
                Set-Content -LiteralPath (Join-Path $OutputDir "GitTrackedFiles.txt") -Encoding UTF8

            git ls-files --others --exclude-standard |
                Set-Content -LiteralPath (Join-Path $OutputDir "GitUntrackedFiles.txt") -Encoding UTF8

            git check-ignore -v $files.FullName 2>$null |
                Set-Content -LiteralPath (Join-Path $OutputDir "GitIgnoredFiles.txt") -Encoding UTF8
        } else {
            "The selected folder is not inside a Git working tree." |
                Set-Content -LiteralPath (Join-Path $OutputDir "GitStatus.txt") -Encoding UTF8
        }
    }
    finally {
        Pop-Location
    }
} else {
    "Git was not found in PATH." |
        Set-Content -LiteralPath (Join-Path $OutputDir "GitStatus.txt") -Encoding UTF8
}

# Summary.
$totalSize = ($files | Measure-Object Length -Sum).Sum
if ($null -eq $totalSize) { $totalSize = 0 }

@"
DeskPulse Folder Scan
=====================
Root: $RootPath
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Folders: $(@($folders).Count)
Files:   $(@($files).Count)
Total size: $([math]::Round($totalSize / 1MB, 2)) MB

This scan is read-only. No files or folders were deleted or modified,
apart from creating this scan output folder and ZIP archive.
"@ | Set-Content -LiteralPath (Join-Path $OutputDir "Summary.txt") -Encoding UTF8

$zipPath = "$OutputDir.zip"
Compress-Archive -LiteralPath "$OutputDir\*" -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Scan complete."
Write-Host "Upload this ZIP to ChatGPT:"
Write-Host $zipPath
