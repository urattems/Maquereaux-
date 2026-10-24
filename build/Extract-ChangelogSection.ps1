# Extrait la section d'une version du CHANGELOG.md (format "Keep a Changelog")
# Usage : ./build/Extract-ChangelogSection.ps1 -Version 1.0.0 -OutFile notes.md
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$ChangelogPath = "CHANGELOG.md",
    [string]$OutFile = "release-notes.md"
)

$lines = Get-Content -Path $ChangelogPath -Encoding UTF8
$header = "^## \[" + [regex]::Escape($Version) + "\]"
$inSection = $false
$result = New-Object System.Collections.Generic.List[string]

foreach ($line in $lines) {
    if ($line -match $header) { $inSection = $true; continue }
    # La section s'arrête au prochain titre de version
    if ($inSection -and $line -match "^## \[") { break }
    if ($inSection) { $result.Add($line) }
}

if ($result.Count -eq 0) {
    Write-Warning "Aucune section trouvée pour la version $Version dans $ChangelogPath"
    $result.Add("Release $Version — voir CHANGELOG.md")
}

$result.Add("")
$result.Add("---")
$result.Add("📥 **Installation** : télécharge ``Maquereaux.exe`` ci-dessous et double-clique. Aucun .NET à installer (exe autonome win-x64).")
$result.Add("⚠️ Windows SmartScreen peut afficher « Windows a protégé votre ordinateur » (exe non signé) : *Informations complémentaires* → *Exécuter quand même*.")

Set-Content -Path $OutFile -Value $result -Encoding UTF8
Write-Host "Notes de release écrites dans $OutFile ($($result.Count) lignes)"
