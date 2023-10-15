#!/usr/bin/env bash
set -e

git clean -xdf
dotnet nuget locals all --clear
