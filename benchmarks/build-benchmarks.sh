#!/bin/bash
cd "$(dirname "$0")/Goose.Benchmarks"
dotnet restore
dotnet build -c Release
