#!/bin/bash
# Build script for creating Windows .exe
# This script publishes the application as a self-contained Windows executable

CONFIGURATION="Release"
RUNTIME="win-x64"
SELF_CONTAINED="true"

echo "Building PrinterAPP for Windows..."
echo "Configuration: $CONFIGURATION"
echo "Runtime: $RUNTIME"
echo "Self-Contained: $SELF_CONTAINED"
echo ""

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean PrinterAPP/PrinterAPP.csproj -c $CONFIGURATION

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore PrinterAPP/PrinterAPP.csproj

# Publish the application
echo "Publishing application..."
dotnet publish PrinterAPP/PrinterAPP.csproj \
    -c $CONFIGURATION \
    -r $RUNTIME \
    --self-contained $SELF_CONTAINED \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "./publish/$RUNTIME"

if [ $? -eq 0 ]; then
    echo ""
    echo "Build completed successfully!"
    echo "Output location: ./publish/$RUNTIME"
    echo ""
    echo "You can find the executable at:"
    echo "  ./publish/$RUNTIME/PrinterAPP.exe"
else
    echo ""
    echo "Build failed with exit code $?"
    exit $?
fi
