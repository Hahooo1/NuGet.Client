// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Build", "CA1031:Modify 'InitializeTypes' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.ExtensionManagerShim.InitializeTypes(System.Action{System.String})")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'CreateMetadata' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.PackageManagementHelpers.CreateMetadata(System.String,NuGet.Packaging.Core.PackageIdentity)~NuGet.VisualStudio.IVsPackageMetadata")]
[assembly: SuppressMessage("Build", "CA1822:Member GetExtensionRepositoryPath does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.PreinstalledPackageInstaller.GetExtensionRepositoryPath(System.String,System.Object,System.Action{System.String})~System.String")]
[assembly: SuppressMessage("Build", "CA1822:Member GetRegistryRepositoryPath does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.PreinstalledPackageInstaller.GetRegistryRepositoryPath(System.String,System.Collections.Generic.IEnumerable{NuGet.VisualStudio.IRegistryKey},System.Action{System.String})~System.String")]
[assembly: SuppressMessage("Build", "CA1801:Parameter provider of method AddFromExtension is never used. Remove the parameter or use it in the method body.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.PreinstalledRepositoryProvider.AddFromExtension(NuGet.Protocol.Core.Types.ISourceRepositoryProvider,System.String)")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'RestorePackages' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.VsPackageRestorer.RestorePackages(EnvDTE.Project)")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'MigrateProjectToPackageRefAsync' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.VsProjectJsonToPackageReferenceMigrator.MigrateProjectToPackageRefAsync(System.String)~System.Threading.Tasks.Task{System.Object}")]
[assembly: SuppressMessage("Build", "CA1822:Member RunDesignTimeBuildAsync does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.VsTemplateWizard.RunDesignTimeBuildAsync(EnvDTE.Project)")]
[assembly: SuppressMessage("Build", "CA1822:Member ErrorHandler does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~P:NuGet.VisualStudio.Implementation.Extensibility.VsPackageInstaller.ErrorHandler")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.VsPackageInstallerEvents.#ctor(NuGet.ProjectManagement.IPackageEventsProvider,NuGet.VisualStudio.Telemetry.INuGetTelemetryProvider)")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.SolutionExplorer.Models.AssetsFileDependenciesSnapshot.TryResolvePackagePath(System.String,System.String,System.String@)~System.Boolean")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.SolutionExplorer.Models.AssetsFileDependenciesSnapshot.UpdateFromAssetsFile(System.String)~NuGet.VisualStudio.SolutionExplorer.Models.AssetsFileDependenciesSnapshot")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.NuGetProjectService.GetInstalledPackagesAsync(NuGet.ProjectManagement.MSBuildNuGetProject,System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.ValueTuple{NuGet.VisualStudio.Contracts.InstalledPackageResultStatus,System.Collections.Generic.IReadOnlyCollection{NuGet.VisualStudio.Contracts.NuGetInstalledPackage}}}")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.VsPackageInstaller.InstallInternalCoreAsync(NuGet.PackageManagement.NuGetPackageManager,NuGet.PackageManagement.GatherCache,NuGet.ProjectManagement.NuGetProject,NuGet.Packaging.Core.PackageIdentity,System.Collections.Generic.IEnumerable{NuGet.Protocol.Core.Types.SourceRepository},NuGet.VisualStudio.VSAPIProjectContext,System.Boolean,System.Boolean,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.VsPackageInstallerEvents.NotifyDelegates(NuGet.VisualStudio.PackageOperationEventArgs,System.Delegate[])")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.Implementation.Extensibility.VsPackageInstallerProjectEvents.NotifyDelegates(NuGet.PackageManagement.PackageProjectEventArgs,System.Delegate[])")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.VsTemplateWizard.GetSolutionDirectoryFromDte(EnvDTE.DTE)~System.String")]
