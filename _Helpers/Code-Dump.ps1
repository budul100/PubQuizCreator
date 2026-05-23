param(
    [string]$root          = "",
    [string]$projectFilter = "",   # e.g. "MyApp.Core" — filters to that subfolder only
    [string[]]$excludeFiles = @()  # e.g. @("appsettings*.json", "*.Designer.cs")
)

$excludeFiles = $excludeFiles | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' }

$extensions  = @("sln", "csproj", "cs", "axaml", "cshtml", "razor", "sql", "yml", "json", "config", "html", "css", "ts", "js", "xml")
$extraExts   = @("razor.cs", "config.js", "js.map")   # compound extensions handled separately
$excludeDirs = @("bin", "obj", "dist", ".git", "node_modules")
$maxChars    = 120000

# Determine root
$root = if ($root -ne "") {
    (Resolve-Path $root).Path
} else {
    Split-Path $PSScriptRoot -Parent
}

# Determine scan root — full solution or a single project subfolder
$scanRoot = if ($projectFilter -ne "") {
    Join-Path $root $projectFilter
} else {
    $root
}

if (-not (Test-Path $scanRoot)) {
    Write-Host "ERROR: Project folder not found: $scanRoot"
    exit 1
}

$slnFile      = Get-ChildItem -Path $root -Filter "*.sln" -File | Select-Object -First 1
$solutionName = if ($slnFile) { [System.IO.Path]::GetFileNameWithoutExtension($slnFile.Name) } else { Split-Path $root -Leaf }

$outputLabel = if ($projectFilter -ne "") { "${solutionName}_${projectFilter}" } else { $solutionName }
$outputBase  = Join-Path $root "${outputLabel}_Code"

# --- Recursive file collection (skips excluded dirs entirely) ---
function Get-FilesFiltered {
    param([string]$Path)
    foreach ($item in Get-ChildItem -Path $Path) {
        if ($item.PSIsContainer) {
            if ($excludeDirs -notcontains $item.Name) {
                Get-FilesFiltered -Path $item.FullName
            }
            # Excluded dir → skip entirely, no recursion
        } else {
            $item
        }
    }
}

$allFiles = Get-FilesFiltered -Path $scanRoot | Where-Object {
    $name = $_.Name

    # Exclude by pattern
    if ($excludeFiles | Where-Object { $name -like $_ }) { return $false }

    # Match compound extensions first (e.g. razor.cs, js.map)
    foreach ($ext in $extraExts) {
        if ($name -like "*.$ext") { return $true }
    }

    # Match simple extensions
    $simpleExt = $_.Extension.TrimStart('.')
    if ($extensions -contains $simpleExt) { return $true }

    return $false
} | Sort-Object FullName

$totalFiles = $allFiles.Count
Write-Host "Found $totalFiles file(s) to process (scan root: $scanRoot)..."

# --- Collect all content blocks ---
$allBlocks = [System.Collections.Generic.List[string]]::new()

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($root.Length).TrimStart('\', '/')
    $lang         = $file.Extension.TrimStart('.')
    $content      = Get-Content $file.FullName -Raw -Encoding UTF8
    if ($null -eq $content) { $content = "" }
    $block = "## FILE: $relativePath`n`n``````$lang`n$content`n``````" + "`n`n"
    $allBlocks.Add($block)
}

# --- Preamble helper ---
function Get-Preamble {
    param([int]$ChunkIndex, [int]$TotalChunks, [string]$SolutionName)
    return @"
<!--
INSTRUCTIONS FOR AI:
- This is chunk $ChunkIndex of $TotalChunks of the '$SolutionName' codebase export.
- Do NOT start any analysis, summary, or response until ALL $TotalChunks chunks have been provided.
- After each chunk except the last, simply confirm receipt (e.g. "Chunk $ChunkIndex of $TotalChunks received. Please continue.").
- Begin your analysis only after the user explicitly confirms that all chunks have been uploaded.
-->

"@
}

# --- Pre-count chunks (including preamble length) ---
# We use a placeholder preamble length based on a worst-case estimate.
# Actual preamble is written correctly in the second pass.
$dummyPreamble = Get-Preamble -ChunkIndex 999 -TotalChunks 999 -SolutionName $outputLabel
$preambleLen   = $dummyPreamble.Length

$tempSize  = $preambleLen
$tempIndex = 1
foreach ($block in $allBlocks) {
    if (($tempSize + $block.Length) -gt $maxChars -and $tempSize -gt $preambleLen) {
        $tempIndex++
        $tempSize = $preambleLen
    }
    $tempSize += $block.Length
}
$totalChunks = $tempIndex

# --- Write chunks ---
$fileIndex      = 1
$currentSize    = 0
$currentFile    = "${outputBase}_${fileIndex}.md"
$filesProcessed = 0

$preamble    = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $outputLabel
Set-Content $currentFile $preamble -Encoding UTF8
$currentSize = $preamble.Length

foreach ($block in $allBlocks) {
    $filesProcessed++
    Write-Progress -Activity "Dumping project files" `
                   -Status "Writing block $filesProcessed / $totalFiles" `
                   -PercentComplete (($filesProcessed / $totalFiles) * 100)

    if (($currentSize + $block.Length) -gt $maxChars -and $currentSize -gt $preamble.Length) {
        Write-Host "  -> Rolling over to chunk $($fileIndex + 1) (current: $currentSize chars)"
        $fileIndex++
        $currentFile = "${outputBase}_${fileIndex}.md"
        $preamble    = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $outputLabel
        Set-Content $currentFile $preamble -Encoding UTF8
        $currentSize = $preamble.Length
    }

    Add-Content $currentFile $block -Encoding UTF8
    $currentSize += $block.Length
}

Write-Progress -Activity "Dumping project files" -Completed
Write-Host "Done. $fileIndex chunk(s) created, $filesProcessed source files included."
