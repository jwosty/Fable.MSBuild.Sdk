#!/usr/bin/env bash
set -e

# PROJ=test_projects/FableMinimal/src/App.fsproj
# PROJ=test_projects/FableThreeProj/src/App/App.fsproj
PROJ=test_projects/FableMultitarget/src/App.fsproj

dotnet tool restore
dotnet pack
dotnet build "$PROJ" -v:d -bl:build2.binlog
