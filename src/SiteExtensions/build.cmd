@ECHO OFF
SET RepoRoot=%~dp0..\..

:: Set the default arguments for build
SET __BuildArch=x64
SET processedArgs=
set __UnprocessedBuildArgs=

:Arg_Loop
if "%1" == "" goto ArgsDone

if /i "%1" == "-x64"                 (set __BuildArch=x64&set processedArgs=!processedArgs! %1&shift&goto ArgsDone)
if /i "%1" == "-x86"                 (set __BuildArch=x86&set processedArgs=!processedArgs! %1&shift&goto ArgsDone)

if [!processedArgs!]==[] (
  set __UnprocessedBuildArgs=%__args%
) else (
  set __UnprocessedBuildArgs=%__args%
  for %%t in (!processedArgs!) do (
    set __UnprocessedBuildArgs=!__UnprocessedBuildArgs:*%%t=!
  )
)

:ArgsDone

ECHO Building Microsoft.AspNetCore.Runtime.SiteExtension
CALL %RepoRoot%\build.cmd -arch %__BuildArch% -projects %~dp0Runtime\Microsoft.AspNetCore.Runtime.SiteExtension.pkgproj /bl:artifacts/log/SiteExtensions-Runtime-%__BuildArch%.binlog %__UnprocessedBuildArgs%

IF %ERRORLEVEL% NEQ 0 (
   EXIT /b %ErrorLevel%
)

ECHO Building LoggingBranch
REM /p:DisableTransitiveFrameworkReferences=true is needed to prevent SDK from picking up transitive references to Microsoft.AspNetCore.App as framework references https://github.com/dotnet/sdk/pull/3221
CALL %RepoRoot%\build.cmd -forceCoreMsbuild -arch %__BuildArch% -projects %~dp0LoggingBranch\LB.csproj /p:DisableTransitiveFrameworkReferences=true /bl:artifacts/log/SiteExtensions-LoggingBranch-%__BuildArch%.binlog %__UnprocessedBuildArgs%

IF %ERRORLEVEL% NEQ 0 (
   EXIT /b %ErrorLevel%
)

IF "%__BuildArch%"=="x64" (
	ECHO Building Microsoft.AspNetCore.AzureAppServices.SiteExtension
	CALL %RepoRoot%\build.cmd -forceCoreMsbuild -projects %~dp0LoggingAggregate\src\Microsoft.AspNetCore.AzureAppServices.SiteExtension\Microsoft.AspNetCore.AzureAppServices.SiteExtension.csproj /bl:artifacts/log/SiteExtensions-LoggingAggregate.binlog %__UnprocessedBuildArgs%
)

IF %ERRORLEVEL% NEQ 0 (
   EXIT /b %ErrorLevel%
)

ECHO SiteExtensions successfully built!
