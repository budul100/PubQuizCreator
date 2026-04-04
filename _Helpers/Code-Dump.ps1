$root        = Split-Path $PSScriptRoot -Parent
$slnFile     = Get-ChildItem -Path $root -Filter "*.sln" -File | Select-Object -First 1
$solutionName = if ($slnFile) { [System.IO.Path]::GetFileNameWithoutExtension($slnFile.Name) } else { Split-Path $root -Leaf }
$outputBase  = Join-Path $root "${solutionName}_Code"

$extensions  = @("*.sln", "*.csproj", "*.cs", "*.axaml", "*.html", "*.cshtml", "*.razor", "*.razor.css", "*.yml", "*.json")
$maxChars    = 120000
$excludeDirs = @("bin", "obj", ".git", "node_modules")

$fileIndex      = 1
$currentSize    = 0
$currentFile    = "${outputBase}_${fileIndex}.md"
$filesProcessed = 0

$allFiles = Get-ChildItem -Path $root -Recurse -Include $extensions | Where-Object {
    $path = $_.FullName
    -not ($excludeDirs | Where-Object { $path -match "\\$_\\" })
} | Sort-Object FullName

$totalFiles = $allFiles.Count
Write-Host "Found $totalFiles file(s) to process..."

# --- Pre-scan to determine total chunk count (approximate) ---
# We do a lightweight pass to know the total number of chunks upfront.
# This lets us write the correct preamble to each file.
# For simplicity, we use a two-pass approach: first collect all blocks, then write.

$allBlocks = [System.Collections.Generic.List[string]]::new()

$allFiles | ForEach-Object {
    $relativePath = $_.FullName.Substring($root.Length).TrimStart('\', '/')
    $header  = "## FILE: $relativePath`n`n``````$($_.Extension.TrimStart('.'))`n"
    $content = (Get-Content $_.FullName -Raw -Encoding UTF8) + "`n``````" + "`n`n"
    $allBlocks.Add($header + $content)
}

# --- Determine total chunks ---
$tempSize  = 0
$tempIndex = 1
foreach ($block in $allBlocks) {
    if (($tempSize + $block.Length) -gt $maxChars -and $tempSize -gt 0) {
        $tempIndex++
        $tempSize = 0
    }
    $tempSize += $block.Length
}
$totalChunks = $tempIndex

# --- Helper: build preamble for a given chunk ---
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

# --- Write chunks ---
$fileIndex   = 1
$currentSize = 0
$currentFile = "${outputBase}_${fileIndex}.md"

$preamble = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $solutionName
Set-Content $currentFile $preamble -Encoding UTF8  # Fix: explicit UTF-8 from the start
$currentSize = $preamble.Length

$filesProcessed = 0
foreach ($block in $allBlocks) {
    $filesProcessed++

    Write-Progress -Activity "Dumping project files" `
                   -Status "Writing block $filesProcessed / $totalFiles" `
                   -PercentComplete (($filesProcessed / $totalFiles) * 100)

    if (($currentSize + $block.Length) -gt $maxChars -and $currentSize -gt 0) {
        Write-Host "  -> Rolling over to ${outputBase}_$($fileIndex + 1).md (chunk $fileIndex full at $currentSize chars)"
        $fileIndex++
        $currentFile = "${outputBase}_${fileIndex}.md"
        $preamble = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $solutionName
        Set-Content $currentFile $preamble -Encoding UTF8  # Fix: explicit UTF-8
        $currentSize = $preamble.Length
    }

    Add-Content $currentFile $block -Encoding UTF8
    $currentSize += $block.Length
}

Write-Progress -Activity "Dumping project files" -Completed
Write-Host "Done. $fileIndex file(s) created, $filesProcessed source files included."
