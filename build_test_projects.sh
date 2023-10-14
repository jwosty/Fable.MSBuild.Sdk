#!/usr/bin/env bash
set -e

git clean -xdf
dotnet nuget locals all --clear
dotnet tool restore
dotnet pack
dotnet build test_projects/FableMinimal/src/App.fsproj
