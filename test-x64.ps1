#!/usr/bin/env pwsh
Write-Host "Testing Debug X64"
dotnet test --nologo -c Debug -- RunConfiguration.TargetPlatform=x64 /Parallel
Write-Host "Testing Release X64"
dotnet test --nologo -c Release --collect:"XPlat Code Coverage" -- RunConfiguration.TargetPlatform=x64 /Parallel 