param([String]$version)

Write-Host "Setting version number to: $version"

function BumpVersion {
  param([String]$projectName)
  
  (Get-Content $projectName\Properties\AssemblyInfo.cs -Encoding UTF8) `
      -replace 'AssemblyVersion\("(\.|\d)+"\)', "AssemblyVersion(""$version"")" `
      -replace 'AssemblyFileVersion\("(\.|\d)+"\)', "AssemblyFileVersion(""$version"")" |
    Out-String |
    Out-File $projectName\Properties\AssemblyInfo.cs -Encoding UTF8
}

BumpVersion("AWSPushAndDeploy")
BumpVersion("AWSCreateStack")
BumpVersion("AWSS3Push")

(Get-Content AWSPushAndDeploy\AWSPushAndDeploy.nuspec -Encoding UTF8) `
    -replace '\<version\>(\.|\d)+\</version\>', "<version>$version</version>" |
  Out-String |
  Out-File AWSPushAndDeploy\AWSPushAndDeploy.nuspec -Encoding UTF8
