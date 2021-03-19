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

:: For example:
::  pack_release 0.0.0 beta
:: or
::  pack_release 0.0.0

dotnet build --nologo -c Release --no-incremental -p:VersionPrefix=%1 -p:VersionSuffix=%2
dotnet pack --nologo --no-build -c Release -p:VersionPrefix=%1 -p:VersionSuffix=%2
