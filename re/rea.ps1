# Runs the locally installed rea CLI with a portable Ghidra / JDK / Node toolchain.
# Usage: .\re\rea.ps1 doctor --provider ghidra --json
# Set REA_TOOLS if the toolchain isn't in %USERPROFILE%\tools. Expected layout:
#   ghidra_12.1.4_PUBLIC\, jdk-21*\, node-v24*\, rea\ (npm install --prefix rea rea-agents)
$T = if ($env:REA_TOOLS) { $env:REA_TOOLS } else { Join-Path $env:USERPROFILE "tools" }
$node = Get-ChildItem $T -Directory -Filter "node-v24*" | Select-Object -First 1
$jdk = Get-ChildItem $T -Directory -Filter "jdk-21*" | Select-Object -First 1
$env:Path = "$($node.FullName);$env:Path"
$env:GHIDRA_INSTALL_DIR = Join-Path $T "ghidra_12.1.4_PUBLIC"
$env:JAVA_HOME = $jdk.FullName
$env:REA_ANALYSIS_PROVIDER = "ghidra"
& (Join-Path $T "rea\node_modules\.bin\rea.cmd") @args
