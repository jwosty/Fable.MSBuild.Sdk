#!/usr/bin/env bash
set -e

PROJ=test_projects/FableMinimal/src/App.fsproj
# PROJ=test_projects/FableThreeProj/src/App/App.fsproj

dotnet tool restore
dotnet pack
dotnet build "$PROJ"
