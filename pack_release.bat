::
::  This file is part of the PlgxBuildTasks distribution:
::  https://github.com/walterpg/plgx-build-tasks
::
::  Copyright(C) 2021 Walter Goodwin
::
::  PlgxBuildTasks is free software: you can redistribute it and/or modify
::  it under the terms of the GNU General Public License as published by
::  the Free Software Foundation, either version 3 of the License, or
::  (at your option) any later version.
::
::  PlgxBuildTasks is distributed in the hope that it will be useful,
::  but WITHOUT ANY WARRANTY; without even the implied warranty of
::  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
::  GNU General Public License for more details.
::
::  You should have received a copy of the GNU General Public License
::  along with PlgxBuildTasks.  If not, see <https://www.gnu.org/licenses/>.
::

:: USAGE:
::  pack_release 0.0.0 beta
:: or
::  pack_release 0.0.0

::
:: Procedure for creating a new package release:
:: 1) Commit and tag the release, e.g.
::    `git tag -a v1.0.0-beta -s -m "Version 1.0"`
:: 2) Push the tag to remote:
::    `git push origin v1.0.0-beta
:: 3) Run this script.
:: 4) Verify the .npkg output in a tool like NuGetPackageExplorer.
:: 5) Publish to nuget.org.
::

dotnet build --nologo -c Release --no-incremental -p:VersionPrefix=%1 -p:VersionSuffix=%2
dotnet pack --nologo --no-build -c Release -p:VersionPrefix=%1 -p:VersionSuffix=%2
