// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Frameworks;

namespace Dotnet.Integration.Test
{
    internal static class Constants
    {
#if NET9_0
        internal static readonly NuGetFramework DefaultTargetFramework = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version(9, 0, 0, 0));
#else
#error Unknown target framework, update this logic to match what target framework this project uses
#endif

        internal static readonly Uri DotNetPackageSource = new("https://dnceng.pkgs.visualstudio.com/public/_packaging/dotnet7/nuget/v3/index.json");
    }
}
