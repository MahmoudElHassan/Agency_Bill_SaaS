#!/bin/sh
export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-5080}"
exec dotnet Ledgerly.Api.dll
