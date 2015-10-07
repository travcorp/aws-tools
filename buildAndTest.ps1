trap
{
    write-output $_
    exit 666
}

$msbuild = (Join-Path $env:winDir "Microsoft.NET\Framework64\v4.0.30319\MSBuild")

& $msbuild .\TTC.Deployment.sln

& .\packages\NUnit.Runners.2.6.4\tools\nunit-console.exe .\TTC.Deployment.Tests\bin\Release\TTC.Deployment.Tests.dll
