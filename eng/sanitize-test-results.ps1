param(
    [Parameter(Mandatory)]
    [string] $ResultsDirectory
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ResultsDirectory -PathType Container)) {
    Write-Host 'No test-results directory exists; nothing to sanitize.'
    exit 0
}
$resolvedDirectory = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$resultFiles = Get-ChildItem -LiteralPath $resolvedDirectory -File -Filter '*.trx' | Sort-Object Name
$fileIndex = 0
foreach ($resultFile in $resultFiles) {
    [xml]$document = [System.IO.File]::ReadAllText($resultFile.FullName)
    $document.DocumentElement.SetAttribute('name', 'FocusLedger test run')
    # Remove runner identity and local path attributes while preserving test names and outcomes.
    foreach ($attributeName in @('runUser', 'computerName', 'runDeploymentRoot', 'codeBase', 'storage')) {
        foreach ($attribute in @($document.SelectNodes("//@$attributeName"))) {
            $attribute.OwnerElement.RemoveAttribute($attributeName)
        }
    }
    $safePath = Join-Path $resolvedDirectory ('focusledger-tests-{0:D2}.trx' -f $fileIndex)
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $settings.IndentChars = '  '
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
    $writer = [System.Xml.XmlWriter]::Create($safePath, $settings)
    try {
        $document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
    if ($resultFile.FullName -ne $safePath) {
        Remove-Item -LiteralPath $resultFile.FullName -Force
    }
    $fileIndex++
}
# Reject sanitized output if the current runner identity or profile path remains anywhere in TRX content.
$sensitiveValues = @($env:USERNAME, $env:COMPUTERNAME, $env:USERPROFILE) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
foreach ($resultFile in Get-ChildItem -LiteralPath $resolvedDirectory -File -Filter '*.trx') {
    $content = [System.IO.File]::ReadAllText($resultFile.FullName)
    foreach ($sensitiveValue in $sensitiveValues) {
        if ($content.Contains($sensitiveValue, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "$($resultFile.Name) still contains runner identity data."
        }
    }
}
Write-Host "Sanitized $fileIndex test result file(s)."
