#!/bin/bash
dotnet publish -c Release -r win-x64 /p:ShowLinkerSizeComparison=true
dotnet publish -c Release -r win-arm64 /p:ShowLinkerSizeComparison=true
dotnet publish -c Release -r linux-x64 /p:ShowLinkerSizeComparison=true
dotnet publish -c Release -r osx-x64 /p:ShowLinkerSizeComparison=true
dotnet publish -c Release -r osx-arm64 /p:ShowLinkerSizeComparison=true
