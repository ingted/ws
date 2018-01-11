@ECHO OFF
REM NOTE: This file was auto-generated with `IB.exe prepare` from `IntelliFactory.Build`.

setlocal
set PATH=%ProgramFiles(x86)%\MSBuild\14.0\Bin;%WINDIR%\Microsoft.NET\Framework\v4.0.30319;%PATH%
set PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin;%PATH%
set PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin;%PATH%
set PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin;%PATH%
set PATH=%PATH%;%ProgramFiles(x86)%\Microsoft SDKs\F#\4.1\Framework\v4.0
set PATH=%PATH%;%ProgramFiles(x86)%\Microsoft SDKs\F#\4.0\Framework\v4.0
set PATH=%PATH%;%ProgramFiles(x86)%\Microsoft SDKs\F#\3.1\Framework\v4.0
set PATH=%PATH%;%ProgramFiles(x86)%\Microsoft SDKs\F#\3.0\Framework\v4.0
set PATH=%PATH%;%ProgramFiles%\Microsoft SDKs\F#\4.1\Framework\v4.0
set PATH=%PATH%;%ProgramFiles%\Microsoft SDKs\F#\4.0\Framework\v4.0
set PATH=%PATH%;%ProgramFiles%\Microsoft SDKs\F#\3.1\Framework\v4.0
set PATH=%PATH%;%ProgramFiles%\Microsoft SDKs\F#\3.0\Framework\v4.0
set PATH=%PATH%;tools\NuGet
msbuild Owin.WebSocket\Owin.WebSocket.csproj
nuget install IntelliFactory.Build -pre -ExcludeVersion -o tools\packages
fsi.exe --exec build-zafir.fsx %*
