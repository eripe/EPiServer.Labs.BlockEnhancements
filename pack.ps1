$defaultVersion="1.0.0"
$workingDirectory = Get-Location
function ZipCurrentModule
{
    Param ([String]$moduleName)
    Robocopy.exe $defaultVersion\ $version\ /S
    ((Get-Content -Path module.config -Raw) -Replace $defaultVersion, $version ) | Set-Content -Path module.config
    "packages\7-Zip.CommandLine.18.1.0\tools\7za.exe a $moduleName.zip $version Views module.config"
    git checkout module.config
    Remove-Item $version -Force -Recurse
}

$fullVersion=[System.Reflection.Assembly]::LoadFrom("src\alloy\bin\EPiServer.Labs.BlockEnhancements.dll").GetName().Version
$version="$($fullVersion.major).$($fullVersion.minor).$($fullVersion.build)"
Write-Host "Creating nuget with $version version"

Set-Location src\alloy\modules\_protected\episerver-labs-block-enhancements
ZipCurrentModule -moduleName episerver-labs-block-enhancements
Set-Location $workingDirectory
build\tools\nuget.exe pack src\alloy\EPiServer.Labs.BlockEnhancements.nuspec -Version $version
