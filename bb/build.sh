#!/bin/bash
dotnet publish -c Release -r win10-x64 /p:ShowLinkerSizeComparison=true
dotnet publish -c Release -r linux-x64 /p:ShowLinkerSizeComparison=true
