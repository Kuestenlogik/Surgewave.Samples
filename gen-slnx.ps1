Set-Location $PSScriptRoot
$lines = @('<Solution>')
$lines += '  <Folder Name="/src/">'

# Scan for csproj files recursively in src/
Get-ChildItem src -Recurse -Filter *.csproj | ForEach-Object {
    $relativePath = $_.FullName.Substring((Get-Location).Path.Length + 1).Replace('\', '/')
    $lines += "    <Project Path=`"$relativePath`" />"
}

$lines += '  </Folder>'
$lines += '</Solution>'
$lines | Out-File -FilePath Kuestenlogik.Surgewave.Samples.slnx -Encoding utf8
Write-Host "Generated Kuestenlogik.Surgewave.Samples.slnx with $($lines.Count) lines"
