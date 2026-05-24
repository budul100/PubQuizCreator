param(
    [string]$root           = "",
    [string]$projectFilter  = "",        # e.g. "MyApp.Core" -- filters to that subfolder only
    [string[]]$excludeFiles = @(),       # e.g. @("appsettings*.json", "*.Designer.cs")
    [string[]]$includeFiles = @()        # absolute or relative paths to always include (e.g. "wwwroot\lib\bootstrap.min.css")
)

$excludeFiles = $excludeFiles | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' }
$includeFiles = $includeFiles | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' }

$extensions  = @("sln", "csproj", "cs", "axaml", "cshtml", "razor", "sql", "yml", "json", "config", "html", "css", "ts", "js", "xml")
$extraExts   = @("razor.cs", "config.js", "js.map")
$excludeDirs = @("bin", "obj", "dist", ".git", "node_modules")
$maxChars    = 120000

# Known minified/binary-like extensions -- included as reference only, content skipped
$refOnlyPatterns = @("*.min.css", "*.min.js", "*.js.map")

# --- Determine roots ---
$root = if ($root -ne "") {
    (Resolve-Path $root).Path
} else {
    Split-Path $PSScriptRoot -Parent
}

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
$outputLabel  = if ($projectFilter -ne "") { "${solutionName}_${projectFilter}" } else { $solutionName }
$outputBase   = Join-Path $root "${outputLabel}_Code"

# --- Recursive file collection ---
function Get-FilesFiltered {
    param([string]$Path)
    foreach ($item in Get-ChildItem -Path $Path) {
        if ($item.PSIsContainer) {
            if ($excludeDirs -notcontains $item.Name) {
                Get-FilesFiltered -Path $item.FullName
            }
        } else {
            $item
        }
    }
}

$scannedFiles = Get-FilesFiltered -Path $scanRoot | Where-Object {
    $name = $_.Name
    if ($excludeFiles | Where-Object { $name -like $_ }) { return $false }
    foreach ($ext in $extraExts) {
        if ($name -like "*.$ext") { return $true }
    }
    $simpleExt = $_.Extension.TrimStart('.')
    if ($extensions -contains $simpleExt) { return $true }
    return $false
} | Sort-Object FullName

# Resolve $includeFiles to FileInfo objects (skip duplicates already in scanned set)
$scannedPaths  = $scannedFiles | ForEach-Object { $_.FullName }
$explicitFiles = $includeFiles | ForEach-Object {
    $p = if ([System.IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
    if (Test-Path $p) {
        $fi = Get-Item $p
        if ($scannedPaths -notcontains $fi.FullName) { $fi }
    } else {
        Write-Host "WARNING: includeFile not found: $p"
    }
} | Where-Object { $_ -ne $null }

$allFiles   = (@($scannedFiles) + @($explicitFiles)) | Sort-Object FullName
$totalFiles = $allFiles.Count
Write-Host "Found $totalFiles file(s) to process (scan root: $scanRoot)..."

# --- Helper: is file reference-only? ---
function Is-RefOnly {
    param([System.IO.FileInfo]$File)
    foreach ($pat in $refOnlyPatterns) {
        if ($File.Name -like $pat) { return $true }
    }
    return $false
}

# --- Helper: check for non-printable (binary) content ---
function Has-BinaryContent {
    param([string]$Content)
    $sample = $Content.Substring(0, [Math]::Min(1000, $Content.Length))
    return ($sample -match '[\x00-\x08\x0B\x0C\x0E-\x1F]')
}

# --- ASCII tree builder ---
function Build-Tree {
    param([string[]]$RelativePaths)

    $lines = [System.Collections.Generic.List[string]]::new()

    function Add-Node {
        param($dict, [string[]]$parts)
        if ($parts.Count -eq 0) { return }
        $head = $parts[0]
        if (-not $dict.ContainsKey($head)) { $dict[$head] = @{} }
        if ($parts.Count -gt 1) {
            Add-Node -dict $dict[$head] -parts ($parts | Select-Object -Skip 1)
        } else {
            $dict[$head]["__file__"] = $true
        }
    }

    $treeDict = @{}
    foreach ($p in $RelativePaths) {
        $parts = $p -split '[/\\]'
        Add-Node -dict $treeDict -parts $parts
    }

    function Render-Node {
        param($dict, [string]$prefix)
        $keys  = $dict.Keys | Where-Object { $_ -ne "__file__" } | Sort-Object
        $count = @($keys).Count
        $i     = 0
        foreach ($key in $keys) {
            $i++
            $isLast      = ($i -eq $count)
            $connector   = if ($isLast) { "+-- " } else { "|-- " }
            $childPrefix = if ($isLast) { "$prefix    " } else { "$prefix|   " }
            $lines.Add("$prefix$connector$key")
            if ($dict[$key].Count -gt 0) {
                Render-Node -dict $dict[$key] -prefix $childPrefix
            }
        }
    }

    $lines.Add($outputLabel + "/")
    Render-Node -dict $treeDict -prefix ""
    return $lines -join "`n"
}

# --- Collect all content blocks + metadata ---
$blockMeta = [System.Collections.Generic.List[hashtable]]::new()

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($root.Length).TrimStart('\', '/')
    $lang         = $file.Extension.TrimStart('.')
    $refOnly      = Is-RefOnly -File $file
    $sizeKB       = [Math]::Round($file.Length / 1KB, 1)
    $modified     = $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
    $binary       = $false
    $content      = ""
    $lineCount    = 0

    if (-not $refOnly) {
        $content = Get-Content $file.FullName -Raw -Encoding UTF8
        if ($null -eq $content) { $content = "" }
        if ($content -ne "") {
            if (Has-BinaryContent -Content $content) {
                $binary  = $true
                $content = ""
            } else {
                $lineCount = ($content -split '\n').Count
            }
        }
    }

    $blockMeta.Add(@{
        RelPath  = $relativePath
        Lang     = $lang
        Lines    = $lineCount
        SizeKB   = $sizeKB
        Modified = $modified
        RefOnly  = $refOnly
        Binary   = $binary
        Content  = $content
    })
}

# --- Build content blocks ---
$allBlocks = [System.Collections.Generic.List[string]]::new()

foreach ($meta in $blockMeta) {
    $flags = ""
    if ($meta.RefOnly) { $flags = " | WARNING: reference only - content omitted (minified/binary-like)" }
    if ($meta.Binary)  { $flags = " | WARNING: binary content detected - content omitted" }
    if ($meta.Lines -eq 0 -and -not $meta.RefOnly -and -not $meta.Binary) { $flags += " | (empty file)" }

    $header = "## FILE: $($meta.RelPath)`n<!-- lines: $($meta.Lines) | size: $($meta.SizeKB) KB | modified: $($meta.Modified)$flags -->"

    if ($meta.RefOnly -or $meta.Binary -or $meta.Content -eq "") {
        $block = "$header`n`n"
    } else {
        $block = "$header`n`n``````$($meta.Lang)`n$($meta.Content)`n``````" + "`n`n"
    }

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
- Chunk 1 contains the full file index and directory tree. Use it to locate files across chunks.
-->

"@
}

# --- Pre-count chunks ---
$dummyPreamble = Get-Preamble -ChunkIndex 999 -TotalChunks 999 -SolutionName $outputLabel
$preambleLen   = $dummyPreamble.Length

$tempSize      = $preambleLen
$tempIndex     = 1
$chunkMap      = [System.Collections.Generic.List[hashtable]]::new()
$curChunkFiles = [System.Collections.Generic.List[string]]::new()

foreach ($i in 0..($allBlocks.Count - 1)) {
    $block   = $allBlocks[$i]
    $relPath = $blockMeta[$i].RelPath
    if (($tempSize + $block.Length) -gt $maxChars -and $tempSize -gt $preambleLen) {
        $chunkMap.Add(@{ ChunkIndex = $tempIndex; Files = $curChunkFiles.ToArray() })
        $tempIndex++
        $tempSize      = $preambleLen
        $curChunkFiles = [System.Collections.Generic.List[string]]::new()
    }
    $tempSize += $block.Length
    $curChunkFiles.Add($relPath)
}
$chunkMap.Add(@{ ChunkIndex = $tempIndex; Files = $curChunkFiles.ToArray() })
$totalChunks = $tempIndex

# --- Build index (goes into chunk 1 header) ---
$indexLines = [System.Collections.Generic.List[string]]::new()
$indexLines.Add("# Codebase Export: $outputLabel")
$indexLines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm') | Files: $totalFiles | Chunks: $totalChunks")
$indexLines.Add("")
$indexLines.Add("## Directory Structure")
$indexLines.Add('```')
$relPaths = $blockMeta | ForEach-Object { $_.RelPath }
$indexLines.Add((Build-Tree -RelativePaths $relPaths))
$indexLines.Add('```')
$indexLines.Add("")
$indexLines.Add("## File Index")
$indexLines.Add("")
$indexLines.Add("| File | Chunk | Lines | Size | Modified |")
$indexLines.Add("|------|-------|-------|------|----------|")

# Build lookup: file -> chunk
$fileChunkLookup = @{}
foreach ($entry in $chunkMap) {
    foreach ($f in $entry.Files) {
        $fileChunkLookup[$f] = $entry.ChunkIndex
    }
}

foreach ($meta in $blockMeta) {
    $chunkNum = $fileChunkLookup[$meta.RelPath]
    $flags    = ""
    if ($meta.RefOnly) { $flags = " *(ref only)*" }
    if ($meta.Binary)  { $flags = " *(binary)*" }
    if ($meta.Lines -eq 0 -and -not $meta.RefOnly -and -not $meta.Binary) { $flags = " *(empty)*" }
    $indexLines.Add("| ``$($meta.RelPath)`` | $chunkNum | $($meta.Lines) | $($meta.SizeKB) KB | $($meta.Modified)$flags |")
}

$indexLines.Add("")
$indexContent = $indexLines -join "`n"

# --- Write chunks ---
$fileIndex     = 1
$currentFile   = "${outputBase}_${fileIndex}.md"
$preamble      = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $outputLabel
$chunk1Open    = $preamble + $indexContent + "`n`n"
Set-Content $currentFile $chunk1Open -Encoding UTF8
$currentSize   = $chunk1Open.Length
$filesProcessed = 0

foreach ($i in 0..($allBlocks.Count - 1)) {
    $block = $allBlocks[$i]
    $filesProcessed++
    Write-Progress -Activity "Dumping project files" `
                   -Status "Writing block $filesProcessed / $totalFiles" `
                   -PercentComplete (($filesProcessed / $totalFiles) * 100)

    if (($currentSize + $block.Length) -gt $maxChars -and $currentSize -gt $preamble.Length) {
        $manifest = "`n---`n**Files in this chunk:** " + ($chunkMap[$fileIndex - 1].Files -join ", ") + "`n"
        Add-Content $currentFile $manifest -Encoding UTF8

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

# Manifest for last chunk
$manifest = "`n---`n**Files in this chunk:** " + ($chunkMap[$fileIndex - 1].Files -join ", ") + "`n"
Add-Content $currentFile $manifest -Encoding UTF8

Write-Progress -Activity "Dumping project files" -Completed
Write-Host "Done. $fileIndex chunk(s) created, $filesProcessed source files included."
