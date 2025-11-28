#!/bin/bash
cd "$(dirname "$0")/Goose.Benchmarks"
dotnet run -c Release -- "$@"
