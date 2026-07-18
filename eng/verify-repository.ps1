$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$textExtensions = @('.config', '.cs', '.csproj', '.json', '.md', '.props', '.ps1', '.slnx', '.txt', '.yml', '.yaml')
$textFileNames = @('.editorconfig', '.gitattributes', '.gitignore')
$allFiles = Get-ChildItem -LiteralPath $projectRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](?:\.git|bin|obj)[\\/]'
}
$textFiles = $allFiles | Where-Object {
    $textExtensions -contains $_.Extension.ToLowerInvariant() -or $textFileNames -contains $_.Name
}
$violations = [System.Collections.Generic.List[string]]::new()
# Verify Windows-native line endings and whitespace for every repository text artifact.
foreach ($file in $textFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($content, '(?<!\r)\n|\r(?!\n)')) {
        $violations.Add("$($file.FullName): non-CRLF line ending")
    }
    if ([regex]::IsMatch($content, '(?m)[ \t]+$')) {
        $violations.Add("$($file.FullName): trailing whitespace")
    }
}
# Verify the project-specific C# style rules that are not fully covered by dotnet format.
$csharpFiles = $allFiles | Where-Object Extension -eq '.cs'
foreach ($file in $csharpFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($content, '/\*|\*/|///|\bprivate\b')) {
        $violations.Add("$($file.FullName): prohibited comment or optional private modifier")
    }
    $braceDepth = 0
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadAllLines($file.FullName)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -and $braceDepth -ge 1) {
            $violations.Add(('{0}:{1}: blank line inside a type or method' -f $file.FullName, $lineNumber))
        }
        $braceDepth += ([regex]::Matches($line, '\{')).Count
        $braceDepth -= ([regex]::Matches($line, '\}')).Count
    }
}
# Verify Central Package Management and the required NUnit test infrastructure.
$projectFiles = $allFiles | Where-Object { $_.Extension -in '.csproj', '.props' }
foreach ($file in $projectFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($content, '<PackageReference[^>]*Version\s*=')) {
        $violations.Add("$($file.FullName): project-level package version")
    }
    if ([regex]::IsMatch($content, '(?i)xunit')) {
        $violations.Add("$($file.FullName): xUnit reference")
    }
}
if ($violations.Count -gt 0) {
    throw [string]::Join([Environment]::NewLine, $violations)
}
Write-Host 'Repository policy verification passed.'
