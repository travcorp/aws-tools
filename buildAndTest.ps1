trap
{
    write-output $_
    exit 666
}

$msbuild = (Join-Path ${env:ProgramFiles(x86)} "MSBuild\14.0\Bin")

& $msbuild  .\TTC.Deployment.sln /p:Configuration=Release /p:VisualStudioVersion=14.0
if (-not $?) {
  throw "Failed to compile the solution"
}

& .\packages\NUnit.ConsoleRunner.3.2.0\tools\nunit3-console.exe .\TTC.Deployment.Tests\bin\Release\TTC.Deployment.Tests.dll --labels=all
if (-not $?) {
  throw "NUnit failed"
}