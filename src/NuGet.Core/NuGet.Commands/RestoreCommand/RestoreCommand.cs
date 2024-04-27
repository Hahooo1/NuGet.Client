//#define verboseLog
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands.Restore.Utility;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        private readonly RestoreCollectorLogger _logger;

        private readonly RestoreRequest _request;

        private readonly LockFileBuilderCache _lockFileBuilderCache;

        private bool _success;

        private Guid _operationId;

        private readonly Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> _includeFlagGraphs
            = new Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>>();

        public Guid ParentId { get; }

        private const string ProjectRestoreInformation = nameof(ProjectRestoreInformation);

        // status names for ProjectRestoreInformation
        private const string ErrorCodes = nameof(ErrorCodes);
        private const string WarningCodes = nameof(WarningCodes);
        private const string RestoreSuccess = nameof(RestoreSuccess);
        private const string ProjectFilePath = nameof(ProjectFilePath);
        private const string IsCentralVersionManagementEnabled = nameof(IsCentralVersionManagementEnabled);
        private const string TotalUniquePackagesCount = nameof(TotalUniquePackagesCount);
        private const string NewPackagesInstalledCount = nameof(NewPackagesInstalledCount);
        private const string SourcesCount = nameof(SourcesCount);
        private const string HttpSourcesCount = nameof(HttpSourcesCount);
        private const string LocalSourcesCount = nameof(LocalSourcesCount);
        private const string FallbackFoldersCount = nameof(FallbackFoldersCount);

        // no-op data names
        private const string NoOpDuration = nameof(NoOpDuration);
        private const string NoOpResult = nameof(NoOpResult);
        private const string NoOpCacheFileEvaluateDuration = nameof(NoOpCacheFileEvaluateDuration);
        private const string NoOpCacheFileEvaluationResult = nameof(NoOpCacheFileEvaluationResult);
        private const string NoOpRestoreOutputEvaluationDuration = nameof(NoOpRestoreOutputEvaluationDuration);
        private const string NoOpRestoreOutputEvaluationResult = nameof(NoOpRestoreOutputEvaluationResult);
        private const string NoOpReplayLogsDuration = nameof(NoOpReplayLogsDuration);
        private const string NoOpCacheFileAgeDays = nameof(NoOpCacheFileAgeDays);

        // lock file data names
        private const string EvaluateLockFileDuration = nameof(EvaluateLockFileDuration);
        private const string ValidatePackagesShaDuration = nameof(ValidatePackagesShaDuration);
        private const string IsLockFileEnabled = nameof(IsLockFileEnabled);
        private const string ReadLockFileDuration = nameof(ReadLockFileDuration);
        private const string ValidateLockFileDuration = nameof(ValidateLockFileDuration);
        private const string IsLockFileValidForRestore = nameof(IsLockFileValidForRestore);
        private const string LockFileEvaluationResult = nameof(LockFileEvaluationResult);

        // core restore data names
        private const string GenerateRestoreGraphDuration = nameof(GenerateRestoreGraphDuration);
        private const string CreateRestoreTargetGraphDuration = nameof(CreateRestoreTargetGraphDuration);
        private const string CreateAdditionalRestoreTargetGraphDuration = nameof(CreateAdditionalRestoreTargetGraphDuration);
        private const string GenerateAssetsFileDuration = nameof(GenerateAssetsFileDuration);
        private const string ValidateRestoreGraphsDuration = nameof(ValidateRestoreGraphsDuration);
        private const string CreateRestoreResultDuration = nameof(CreateRestoreResultDuration);
        private const string IsCentralPackageTransitivePinningEnabled = nameof(IsCentralPackageTransitivePinningEnabled);

        // PackageSourceMapping names
        private const string PackageSourceMappingIsMappingEnabled = "PackageSourceMapping.IsMappingEnabled";

        // NuGetAudit names
        private const string AuditEnabled = "Audit.Enabled";
        private const string AuditLevel = "Audit.Level";
        private const string AuditMode = "Audit.Mode";
        private const string AuditSuppressedAdvisoriesDefinedCount = "Audit.SuppressedAdvisories.Defined.Count";
        private const string AuditSuppressedAdvisoriesTotalWarningsSuppressedCount = "Audit.SuppressedAdvisories.TotalWarningsSuppressed.Count";
        private const string AuditSuppressedAdvisoriesDistinctAdvisoriesSuppressedCount = "Audit.SuppressedAdvisories.DistinctAdvisoriesSuppressed.Count";
        private const string AuditDataSources = "Audit.DataSources";
        private const string AuditDirectVulnerabilitiesPackages = "Audit.Vulnerability.Direct.Packages";
        private const string AuditDirectVulnerabilitiesCount = "Audit.Vulnerability.Direct.Count";
        private const string AuditDirectVulnerabilitySev0 = "Audit.Vulnerability.Direct.Severity0";
        private const string AuditDirectVulnerabilitySev1 = "Audit.Vulnerability.Direct.Severity1";
        private const string AuditDirectVulnerabilitySev2 = "Audit.Vulnerability.Direct.Severity2";
        private const string AuditDirectVulnerabilitySev3 = "Audit.Vulnerability.Direct.Severity3";
        private const string AuditDirectVulnerabilitySevInvalid = "Audit.Vulnerability.Direct.SeverityInvalid";
        private const string AuditTransitiveVulnerabilitiesPackages = "Audit.Vulnerability.Transitive.Packages";
        private const string AuditTransitiveVulnerabilitiesCount = "Audit.Vulnerability.Transitive.Count";
        private const string AuditTransitiveVulnerabilitySev0 = "Audit.Vulnerability.Transitive.Severity0";
        private const string AuditTransitiveVulnerabilitySev1 = "Audit.Vulnerability.Transitive.Severity1";
        private const string AuditTransitiveVulnerabilitySev2 = "Audit.Vulnerability.Transitive.Severity2";
        private const string AuditTransitiveVulnerabilitySev3 = "Audit.Vulnerability.Transitive.Severity3";
        private const string AuditTransitiveVulnerabilitySevInvalid = "Audit.Vulnerability.Transitive.SeverityInvalid";
        private const string AuditDurationDownload = "Audit.Duration.Download";
        private const string AuditDurationCheck = "Audit.Duration.Check";
        private const string AuditDurationOutput = "Audit.Duration.Output";
        private const string AuditDurationTotal = "Audit.Duration.Total";


        //BSW - variable to control if we use the prototype or not
        private int _usePrototype = -1;
        //

        public RestoreCommand(RestoreRequest request)
        {
            //BSW - set
            string subProtoNugValue = Environment.GetEnvironmentVariable("SubProtoNug");
            if (subProtoNugValue != null)
            {
                if (subProtoNugValue == "1")
                    _usePrototype = 1;
                else if (subProtoNugValue == "2")
                    _usePrototype = 2;
                else
                    _usePrototype = 0;
            }
            else
                _usePrototype = 0;
            //BSW

            _request = request ?? throw new ArgumentNullException(nameof(request));
            _lockFileBuilderCache = request.LockFileBuilderCache;

            // Validate the lock file version requested
            if (_request.LockFileVersion < 1 || _request.LockFileVersion > LockFileFormat.Version)
            {
                Debug.Fail($"Lock file version {_request.LockFileVersion} is not supported.");
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(request),
                    message: nameof(request.LockFileVersion));
            }

            var collectorLoggerHideWarningsAndErrors = request.Project.RestoreSettings.HideWarningsAndErrors
                || request.HideWarningsAndErrors;

            var collectorLogger = new RestoreCollectorLogger(_request.Log, collectorLoggerHideWarningsAndErrors);

            collectorLogger.ApplyRestoreInputs(_request.Project);

            _logger = collectorLogger;
            ParentId = request.ParentId;

            _success = !request.AdditionalMessages?.Any(m => m.Level == LogLevel.Error) ?? true;
        }

        public Task<RestoreResult> ExecuteAsync()
        {
            return ExecuteAsync(CancellationToken.None);
        }

        public async Task<RestoreResult> ExecuteAsync(CancellationToken token)
        {
            using (var telemetry = TelemetryActivity.Create(parentId: ParentId, eventName: ProjectRestoreInformation))
            {
                telemetry.TelemetryEvent.AddPiiData(ProjectFilePath, _request.Project.FilePath);

                bool isPackageSourceMappingEnabled = _request.PackageSourceMapping?.IsEnabled ?? false;
                telemetry.TelemetryEvent[PackageSourceMappingIsMappingEnabled] = isPackageSourceMappingEnabled;
                telemetry.TelemetryEvent[SourcesCount] = _request.DependencyProviders.RemoteProviders.Count;
                int httpSourcesCount = _request.DependencyProviders.RemoteProviders.Where(e => e.IsHttp).Count();
                telemetry.TelemetryEvent[HttpSourcesCount] = httpSourcesCount;
                telemetry.TelemetryEvent[LocalSourcesCount] = _request.DependencyProviders.RemoteProviders.Count - httpSourcesCount;
                telemetry.TelemetryEvent[FallbackFoldersCount] = _request.DependencyProviders.FallbackPackageFolders.Count;
                bool isLockFileEnabled = PackagesLockFileUtilities.IsNuGetLockFileEnabled(_request.Project);
                telemetry.TelemetryEvent[IsLockFileEnabled] = isLockFileEnabled;

                _operationId = telemetry.OperationId;

                var isCpvmEnabled = _request.Project.RestoreMetadata?.CentralPackageVersionsEnabled ?? false;
                telemetry.TelemetryEvent[IsCentralVersionManagementEnabled] = isCpvmEnabled;

                if (isCpvmEnabled)
                {
                    var isCentralPackageTransitivePinningEnabled = _request.Project.RestoreMetadata?.CentralPackageTransitivePinningEnabled ?? false;
                    telemetry.TelemetryEvent[IsCentralPackageTransitivePinningEnabled] = isCentralPackageTransitivePinningEnabled;
                }

                var restoreTime = Stopwatch.StartNew();

                // Local package folders (non-sources)
                var localRepositories = new List<NuGetv3LocalRepository>
                {
                    _request.DependencyProviders.GlobalPackages
                };

                localRepositories.AddRange(_request.DependencyProviders.FallbackPackageFolders);

                var contextForProject = CreateRemoteWalkContext(_request, _logger);

                CacheFile cacheFile = null;

                using (telemetry.StartIndependentInterval(NoOpDuration))
                {
                    if (NoOpRestoreUtilities.IsNoOpSupported(_request))
                    {
                        telemetry.StartIntervalMeasure();
                        bool noOp;
                        TimeSpan? cacheFileAge;

                        if (NuGetEventSource.IsEnabled) TraceEvents.CalcNoOpRestoreStart(_request.Project.FilePath);
                        (cacheFile, noOp, cacheFileAge) = EvaluateCacheFile();
                        if (NuGetEventSource.IsEnabled) TraceEvents.CalcNoOpRestoreStop(_request.Project.FilePath);

                        telemetry.TelemetryEvent[NoOpCacheFileEvaluationResult] = noOp;
                        telemetry.EndIntervalMeasure(NoOpCacheFileEvaluateDuration);
                        if (noOp)
                        {
                            telemetry.StartIntervalMeasure();

                            var noOpSuccess = NoOpRestoreUtilities.VerifyRestoreOutput(_request, cacheFile);

                            telemetry.EndIntervalMeasure(NoOpRestoreOutputEvaluationDuration);
                            telemetry.TelemetryEvent[NoOpRestoreOutputEvaluationResult] = noOpSuccess;

                            if (noOpSuccess)
                            {
                                telemetry.StartIntervalMeasure();

                                // Replay Warnings and Errors from an existing lock file in case of a no-op.
                                await MSBuildRestoreUtility.ReplayWarningsAndErrorsAsync(cacheFile.LogMessages, _logger);

                                telemetry.EndIntervalMeasure(NoOpReplayLogsDuration);

                                restoreTime.Stop();
                                telemetry.TelemetryEvent[NoOpResult] = true;
                                telemetry.TelemetryEvent[RestoreSuccess] = _success;
                                telemetry.TelemetryEvent[TotalUniquePackagesCount] = cacheFile.ExpectedPackageFilePaths?.Count ?? -1;
                                telemetry.TelemetryEvent[NewPackagesInstalledCount] = 0;
                                if (cacheFileAge.HasValue) { telemetry.TelemetryEvent[NoOpCacheFileAgeDays] = cacheFileAge.Value.TotalDays; }

                                return new NoOpRestoreResult(
                                    _success,
                                    _request.LockFilePath,
                                    new Lazy<LockFile>(() => LockFileUtilities.GetLockFile(_request.LockFilePath, _logger)),
                                    cacheFile,
                                    _request.Project.RestoreMetadata.CacheFilePath,
                                    _request.ProjectStyle,
                                    restoreTime.Elapsed);
                            }
                        }
                    }
                }
                telemetry.TelemetryEvent[NoOpResult] = false; // Getting here means we did not no-op.

                if (!await AreCentralVersionRequirementsSatisfiedAsync(_request, httpSourcesCount))
                {
                    // the errors will be added to the assets file
                    _success = false;
                }

                if (_request.DependencyProviders.RemoteProviders != null)
                {
                    foreach (var remoteProvider in _request.DependencyProviders.RemoteProviders)
                    {
                        var source = remoteProvider.Source;
                        if (source.IsHttp && !source.IsHttps && !source.AllowInsecureConnections)
                        {
                            await _logger.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1803,
                                string.Format(CultureInfo.CurrentCulture, Strings.Warning_HttpServerUsage, "restore", source.Source)));
                        }
                    }
                }

                _success &= HasValidPlatformVersions();

                // evaluate packages.lock.json file
                var packagesLockFilePath = PackagesLockFileUtilities.GetNuGetLockFilePath(_request.Project);
                var isLockFileValid = false;
                PackagesLockFile packagesLockFile = null;
                var regenerateLockFile = true;

                using (telemetry.StartIndependentInterval(EvaluateLockFileDuration))
                {
                    bool result;
                    (result, isLockFileValid, packagesLockFile) = await EvaluatePackagesLockFileAsync(packagesLockFilePath, contextForProject, telemetry);

                    telemetry.TelemetryEvent[IsLockFileValidForRestore] = isLockFileValid;
                    telemetry.TelemetryEvent[LockFileEvaluationResult] = result;

                    regenerateLockFile = result; // Ensure that the lock file *does not* get rewritten, when the lock file is out of date and the status is false.
                    _success &= result;
                }

                IEnumerable<RestoreTargetGraph> graphs = null;
                IEnumerable<RestoreTargetGraph> prototypeGraphs = null;
                if (_success)
                {
                    using (telemetry.StartIndependentInterval(GenerateRestoreGraphDuration))
                    {
                        long originalTimeMs = -1;
                        long prototypeTimeMs = -1;
                        if ((_usePrototype == 0) || (_usePrototype == 2))
                        {
                            Stopwatch sw = Stopwatch.StartNew();
                            // Restore
                            if (NuGetEventSource.IsEnabled) TraceEvents.BuildRestoreGraphStart(_request.Project.FilePath);
                            graphs = await ExecuteRestoreAsync(
                            _request.DependencyProviders.GlobalPackages,
                            _request.DependencyProviders.FallbackPackageFolders,
                            contextForProject,
                            token,
                            telemetry);
                            if (NuGetEventSource.IsEnabled) TraceEvents.BuildRestoreGraphStop(_request.Project.FilePath);
                            originalTimeMs = sw.ElapsedMilliseconds;
                        }
                        if ((_usePrototype == 1) || (_usePrototype == 2))
                        {
                            Stopwatch sw = Stopwatch.StartNew();
                            // Restore using the prototype
                            if (NuGetEventSource.IsEnabled) TraceEvents.BuildRestoreGraphStart(_request.Project.FilePath);
                            prototypeGraphs = await ExecuteSubProtoTypeRestoreAsync(
                            _request.DependencyProviders.GlobalPackages,
                            _request.DependencyProviders.FallbackPackageFolders,
                            contextForProject,
                            token,
                            telemetry);
                            if (NuGetEventSource.IsEnabled) TraceEvents.BuildRestoreGraphStop(_request.Project.FilePath);
                            prototypeTimeMs = sw.ElapsedMilliseconds;
                        }
                        _logger.LogMinimal($"BSW_Rt: orig={originalTimeMs}, prototype={prototypeTimeMs}, {_request.Project.FilePath}");
                        if (_usePrototype == 1)
                        {
                            _logger.LogMinimal($"***** Using SubProtoNug for {_request.Project.FilePath}");
                            graphs = prototypeGraphs;
                        }
                        if (_usePrototype == 2)
                        {
                            _logger.LogMinimal($"***** Using SubProtoNug flattened graph for {_request.Project.FilePath}");
                            foreach (var originalGraph in graphs)
                            {
                                originalGraph.Flattened = null; // assure we are actually assigning a flattened graph to everything
                                foreach (var prototypeGraph in prototypeGraphs)
                                {
                                    if ((prototypeGraph.Framework == originalGraph.Framework) &&
                                        (prototypeGraph.RuntimeIdentifier == originalGraph.RuntimeIdentifier))
                                    {
                                        originalGraph.Flattened = prototypeGraph.Flattened;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Being in an unsuccessful state before ExecuteRestoreAsync means there was a problem with the
                    // project or we're in locked mode and out of date.
                    // For example, project TFM or package versions couldn't be parsed. Although the minimal
                    // fake package spec generated has no packages requested, it also doesn't have any project TFMs
                    // and will generate validation errors if we tried to call ExecuteRestoreAsync. So, to avoid
                    // incorrect validation messages, don't try to restore. It is however, the responsibility for the
                    // caller of RestoreCommand to have provided at least one AdditionalMessage in RestoreArgs.
                    // The other scenario is when the lock file is not up to date and we're running locked mode.
                    // In that case we want to write a `target` for each target framework to avoid missing target errors from the SDK build tasks.
                    var frameworkRuntimePair = CreateFrameworkRuntimePairs(_request.Project, RequestRuntimeUtility.GetRestoreRuntimes(_request));
                    graphs = frameworkRuntimePair.Select(e =>
                    {
                        return RestoreTargetGraph.Create(_request.Project.RuntimeGraph, Enumerable.Empty<GraphNode<RemoteResolveResult>>(), contextForProject, _logger, e.Framework, e.RuntimeIdentifier);
                    });
                }

                bool auditEnabled = AuditUtility.ParseEnableValue(
                    _request.Project.RestoreMetadata?.RestoreAuditProperties,
                    _request.Project.FilePath,
                    _logger);
                telemetry.TelemetryEvent[AuditEnabled] = auditEnabled ? "enabled" : "disabled";
                if (auditEnabled)
                {
                    await PerformAuditAsync(graphs, telemetry, token);
                }

                telemetry.StartIntervalMeasure();
                // Create assets file
                if (NuGetEventSource.IsEnabled) TraceEvents.BuildAssetsFileStart(_request.Project.FilePath);
                LockFile assetsFile = BuildAssetsFile(
                    _request.ExistingLockFile,
                    _request.Project,
                    graphs,
                    localRepositories,
                    contextForProject);
                if (NuGetEventSource.IsEnabled) TraceEvents.BuildAssetsFileStop(_request.Project.FilePath);
                telemetry.EndIntervalMeasure(GenerateAssetsFileDuration);

                IList<CompatibilityCheckResult> checkResults = null;

                telemetry.StartIntervalMeasure();

                _success &= await ValidateRestoreGraphsAsync(graphs, _logger);

                // Check package compatibility
                checkResults = await VerifyCompatibilityAsync(
                    _request.Project,
                    _includeFlagGraphs,
                    localRepositories,
                    assetsFile,
                    graphs,
                    _request.ValidateRuntimeAssets,
                    _logger);

                if (checkResults.Any(r => !r.Success))
                {
                    _success = false;
                }
                telemetry.EndIntervalMeasure(ValidateRestoreGraphsDuration);


                // Generate Targets/Props files
                var msbuildOutputFiles = Enumerable.Empty<MSBuildOutputFile>();
                string assetsFilePath = null;
                string cacheFilePath = null;

                using (telemetry.StartIndependentInterval(CreateRestoreResultDuration))
                {
                    // Determine the lock file output path
                    assetsFilePath = GetAssetsFilePath(assetsFile);

                    // Determine the cache file output path
                    cacheFilePath = NoOpRestoreUtilities.GetCacheFilePath(_request, assetsFile);

                    // Tool restores are unique since the output path is not known until after restore
                    if (_request.LockFilePath == null
                        && _request.ProjectStyle == ProjectStyle.DotnetCliTool)
                    {
                        _request.LockFilePath = assetsFilePath;
                    }

                    if (contextForProject.IsMsBuildBased)
                    {
                        msbuildOutputFiles = BuildAssetsUtils.GetMSBuildOutputFiles(
                            _request.Project,
                            assetsFile,
                            graphs,
                            localRepositories,
                            _request,
                            assetsFilePath,
                            _success,
                            _logger);
                    }

                    // If the request is for a lower lock file version, downgrade it appropriately
                    DowngradeLockFileIfNeeded(assetsFile);

                    // Revert to the original case if needed
                    await FixCaseForLegacyReaders(graphs, assetsFile, token);

                    // if lock file was still valid then validate package's sha512 hash or else write
                    // the file if enabled.
                    if (isLockFileValid)
                    {
                        telemetry.StartIntervalMeasure();
                        // validate package's SHA512
                        _success &= ValidatePackagesSha512(packagesLockFile, assetsFile);
                        telemetry.EndIntervalMeasure(ValidatePackagesShaDuration);

                        // clear out the existing lock file so that we don't over-write the same file
                        packagesLockFile = null;
                    }
                    else if (isLockFileEnabled)
                    {
                        if (regenerateLockFile)
                        {
                            // generate packages.lock.json file if enabled
                            packagesLockFile = new PackagesLockFileBuilder()
                                .CreateNuGetLockFile(assetsFile);
                        }
                        else
                        {
                            packagesLockFile = null;
                            _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_SkippingPackagesLockFileGeneration, packagesLockFilePath));
                        }
                    }

                    // Write the logs into the assets file
                    var logsEnumerable = _logger.Errors
                        .Select(l => AssetsLogMessage.Create(l));
                    if (_request.AdditionalMessages != null)
                    {
                        logsEnumerable = logsEnumerable.Concat(_request.AdditionalMessages);
                    }
                    var logs = logsEnumerable
                        .ToList();

                    _success &= !logs.Any(l => l.Level == LogLevel.Error);

                    assetsFile.LogMessages = logs;

                    if (cacheFile != null)
                    {
                        cacheFile.Success = _success;
                        cacheFile.ProjectFilePath = _request.Project.FilePath;
                        cacheFile.LogMessages = assetsFile.LogMessages;
                        cacheFile.ExpectedPackageFilePaths = NoOpRestoreUtilities.GetRestoreOutput(_request, assetsFile);
                        telemetry.TelemetryEvent[TotalUniquePackagesCount] = cacheFile?.ExpectedPackageFilePaths.Count;
                    }

                    var errorCodes = ConcatAsString(new HashSet<NuGetLogCode>(logs.Where(l => l.Level == LogLevel.Error).Select(l => l.Code)));
                    var warningCodes = ConcatAsString(new HashSet<NuGetLogCode>(logs.Where(l => l.Level == LogLevel.Warning).Select(l => l.Code)));

                    if (!string.IsNullOrEmpty(errorCodes))
                    {
                        telemetry.TelemetryEvent[ErrorCodes] = errorCodes;
                    }

                    if (!string.IsNullOrEmpty(warningCodes))
                    {
                        telemetry.TelemetryEvent[WarningCodes] = warningCodes;
                    }


                    telemetry.TelemetryEvent[NewPackagesInstalledCount] = graphs.Where(g => !g.InConflict).SelectMany(g => g.Install).Distinct().Count();

                    telemetry.TelemetryEvent[RestoreSuccess] = _success;
                }

                restoreTime.Stop();

                // Create result
                return new RestoreResult(
                    _success,
                    graphs,
                    checkResults,
                    msbuildOutputFiles,
                    assetsFile,
                    _request.ExistingLockFile,
                    assetsFilePath,
                    cacheFile,
                    cacheFilePath,
                    packagesLockFilePath,
                    packagesLockFile,
                    dependencyGraphSpecFilePath: NoOpRestoreUtilities.GetPersistedDGSpecFilePath(_request),
                    dependencyGraphSpec: _request.DependencyGraphSpec,
                    _request.ProjectStyle,
                    restoreTime.Elapsed);
            }
        }

        private async Task PerformAuditAsync(IEnumerable<RestoreTargetGraph> graphs, TelemetryActivity telemetry, CancellationToken token)
        {
            telemetry.StartIntervalMeasure();
            var audit = new AuditUtility(
                _request.Project.RestoreMetadata.RestoreAuditProperties,
                _request.Project.FilePath,
                graphs,
                _request.DependencyProviders.VulnerabilityInfoProviders,
                _logger);
            await audit.CheckPackageVulnerabilitiesAsync(token);

            telemetry.TelemetryEvent[AuditLevel] = (int)audit.MinSeverity;
            telemetry.TelemetryEvent[AuditMode] = AuditUtility.GetString(audit.AuditMode);
            telemetry.TelemetryEvent[AuditSuppressedAdvisoriesDefinedCount] = audit.SuppressedAdvisories?.Count ?? 0;
            telemetry.TelemetryEvent[AuditSuppressedAdvisoriesDistinctAdvisoriesSuppressedCount] = audit.DistinctAdvisoriesSuppressedCount;
            telemetry.TelemetryEvent[AuditSuppressedAdvisoriesTotalWarningsSuppressedCount] = audit.TotalWarningsSuppressedCount;

            if (audit.DirectPackagesWithAdvisory is not null) { AddPackagesList(telemetry, AuditDirectVulnerabilitiesPackages, audit.DirectPackagesWithAdvisory); }
            telemetry.TelemetryEvent[AuditDirectVulnerabilitiesCount] = audit.DirectPackagesWithAdvisory?.Count ?? 0;
            telemetry.TelemetryEvent[AuditDirectVulnerabilitySev0] = audit.Sev0DirectMatches;
            telemetry.TelemetryEvent[AuditDirectVulnerabilitySev1] = audit.Sev1DirectMatches;
            telemetry.TelemetryEvent[AuditDirectVulnerabilitySev2] = audit.Sev2DirectMatches;
            telemetry.TelemetryEvent[AuditDirectVulnerabilitySev3] = audit.Sev3DirectMatches;
            telemetry.TelemetryEvent[AuditDirectVulnerabilitySevInvalid] = audit.InvalidSevDirectMatches;

            if (audit.TransitivePackagesWithAdvisory is not null) { AddPackagesList(telemetry, AuditTransitiveVulnerabilitiesPackages, audit.TransitivePackagesWithAdvisory); }
            telemetry.TelemetryEvent[AuditTransitiveVulnerabilitiesCount] = audit.TransitivePackagesWithAdvisory?.Count ?? 0;
            telemetry.TelemetryEvent[AuditTransitiveVulnerabilitySev0] = audit.Sev0TransitiveMatches;
            telemetry.TelemetryEvent[AuditTransitiveVulnerabilitySev1] = audit.Sev1TransitiveMatches;
            telemetry.TelemetryEvent[AuditTransitiveVulnerabilitySev2] = audit.Sev2TransitiveMatches;
            telemetry.TelemetryEvent[AuditTransitiveVulnerabilitySev3] = audit.Sev3TransitiveMatches;
            telemetry.TelemetryEvent[AuditTransitiveVulnerabilitySevInvalid] = audit.InvalidSevTransitiveMatches;

            telemetry.TelemetryEvent[AuditDataSources] = audit.SourcesWithVulnerabilityData;
            if (audit.DownloadDurationSeconds.HasValue) { telemetry.TelemetryEvent[AuditDurationDownload] = audit.DownloadDurationSeconds.Value; }
            if (audit.CheckPackagesDurationSeconds.HasValue) { telemetry.TelemetryEvent[AuditDurationCheck] = audit.CheckPackagesDurationSeconds.Value; }
            if (audit.GenerateOutputDurationSeconds.HasValue) { telemetry.TelemetryEvent[AuditDurationOutput] = audit.GenerateOutputDurationSeconds.Value; }
            telemetry.EndIntervalMeasure(AuditDurationTotal);

            void AddPackagesList(TelemetryActivity telemetry, string eventName, List<string> packages)
            {
                List<TelemetryEvent> result = new List<TelemetryEvent>(packages.Count);
                foreach (var package in packages)
                {
                    TelemetryEvent packageData = new TelemetryEvent(eventName: string.Empty);
                    packageData.AddPiiData("id", package);
                    result.Add(packageData);
                }

                telemetry.TelemetryEvent.ComplexData[eventName] = result;
            }
        }

        private bool HasValidPlatformVersions()
        {
            IEnumerable<NuGetFramework> badPlatforms = _request.Project.TargetFrameworks
                .Select(frameworkInfo => frameworkInfo.FrameworkName)
                .Where(framework => !string.IsNullOrEmpty(framework.Platform) && (framework.PlatformVersion == FrameworkConstants.EmptyVersion));

            if (badPlatforms.Any())
            {
                _logger.Log(RestoreLogMessage.CreateError(
                    NuGetLogCode.NU1012,
                    string.Format(CultureInfo.CurrentCulture, Strings.Error_PlatformVersionNotPresent, string.Join(", ", badPlatforms))
                ));
                return false;
            }
            else
            {
                return true;
            }
        }

        private async Task<bool> AreCentralVersionRequirementsSatisfiedAsync(RestoreRequest restoreRequest, int httpSourcesCount)
        {
            if (restoreRequest?.Project?.RestoreMetadata == null || !restoreRequest.Project.RestoreMetadata.CentralPackageVersionsEnabled)
            {
                return true;
            }

            IEnumerable<LibraryDependency> dependenciesWithVersionOverride = restoreRequest.Project.TargetFrameworks.SelectMany(tfm => tfm.Dependencies.Where(d => !d.AutoReferenced && d.VersionOverride != null));

            if (restoreRequest.Project.RestoreMetadata.CentralPackageVersionOverrideDisabled)
            {
                // Emit a error if VersionOverride was specified for a package reference but that functionality is disabled
                bool hasVersionOverrides = false;
                foreach (var item in dependenciesWithVersionOverride)
                {
                    await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1013, string.Format(CultureInfo.CurrentCulture, Strings.Error_CentralPackageVersions_VersionOverrideDisabled, item.Name)));
                    hasVersionOverrides = true;
                }

                if (hasVersionOverrides)
                {
                    return false;
                }
            }

            if (!restoreRequest.PackageSourceMapping.IsEnabled && httpSourcesCount > 1)
            {
                // Log a warning if there are more than one configured source and package source mapping is not enabled
                await _logger.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1507, string.Format(CultureInfo.CurrentCulture, Strings.Warning_CentralPackageVersions_MultipleSourcesWithoutPackageSourceMapping, httpSourcesCount, string.Join(", ", restoreRequest.DependencyProviders.RemoteProviders.Where(i => i.IsHttp).Select(i => i.Source.Name)))));
            }

            // The dependencies should not have versions explicitly defined if cpvm is enabled.
            IEnumerable<LibraryDependency> dependenciesWithDefinedVersion = _request.Project.TargetFrameworks.SelectMany(tfm => tfm.Dependencies.Where(d => !d.VersionCentrallyManaged && !d.AutoReferenced && d.VersionOverride == null));
            if (dependenciesWithDefinedVersion.Any())
            {
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1008, string.Format(CultureInfo.CurrentCulture, Strings.Error_CentralPackageVersions_VersionsNotAllowed, string.Join(";", dependenciesWithDefinedVersion.Select(d => d.Name)))));
                return false;
            }
            IEnumerable<LibraryDependency> autoReferencedAndDefinedInCentralFile = _request.Project.TargetFrameworks.SelectMany(tfm => tfm.Dependencies.Where(d => d.AutoReferenced && tfm.CentralPackageVersions.ContainsKey(d.Name)));
            if (autoReferencedAndDefinedInCentralFile.Any())
            {
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1009, string.Format(CultureInfo.CurrentCulture, Strings.Error_CentralPackageVersions_AutoreferencedReferencesNotAllowed, string.Join(";", autoReferencedAndDefinedInCentralFile.Select(d => d.Name)))));

                return false;
            }
            IEnumerable<LibraryDependency> packageReferencedDependenciesWithoutCentralVersionDefined = _request.Project.TargetFrameworks.SelectMany(tfm => tfm.Dependencies.Where(d => d.LibraryRange.VersionRange == null));
            if (packageReferencedDependenciesWithoutCentralVersionDefined.Any())
            {
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1010, string.Format(CultureInfo.CurrentCulture, Strings.Error_CentralPackageVersions_MissingPackageVersion, string.Join(";", packageReferencedDependenciesWithoutCentralVersionDefined.Select(d => d.Name)))));
                return false;
            }

            if (!restoreRequest.Project.RestoreMetadata.CentralPackageFloatingVersionsEnabled)
            {
                var floatingVersionDependencies = _request.Project.TargetFrameworks.SelectMany(tfm => tfm.CentralPackageVersions.Values).Where(cpv => cpv.VersionRange.IsFloating);
                if (floatingVersionDependencies.Any())
                {
                    await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1011, Strings.Error_CentralPackageVersions_FloatingVersionsAreNotAllowed));
                    return false;
                }
            }

            return true;
        }

        private string ConcatAsString<T>(IEnumerable<T> enumerable)
        {
            string result = null;

            if (enumerable != null && enumerable.Any())
            {
                var builder = new StringBuilder();
                foreach (var entry in enumerable)
                {
                    builder.Append(entry.ToString());
                    builder.Append(";");
                }

                result = builder.ToString(0, builder.Length - 1);
            }

            return result;
        }

        /// <summary>
        /// Accounts for using the restore commands on 2 projects living in the same path
        /// </summary>
        private bool VerifyCacheFileMatchesProject(CacheFile cacheFile)
        {
            if (_request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool)
            {
                return true;
            }
            var pathComparer = PathUtility.GetStringComparerBasedOnOS();
            return pathComparer.Equals(cacheFile.ProjectFilePath, _request.Project.FilePath);
        }

        private bool ValidatePackagesSha512(PackagesLockFile lockFile, LockFile assetsFile)
        {
            var librariesLookUp = lockFile.Targets
                .SelectMany(t => t.Dependencies.Where(dep => dep.Type != PackageDependencyType.Project))
                .Distinct(LockFileDependencyIdVersionComparer.Default)
                .ToDictionary(dep => new PackageIdentity(dep.Id, dep.ResolvedVersion), val => val.ContentHash);

            StringBuilder errorMessageBuilder = null;
            foreach (var library in assetsFile.Libraries.Where(lib => lib.Type == LibraryType.Package))
            {
                var package = new PackageIdentity(library.Name, library.Version);

                if (!librariesLookUp.TryGetValue(package, out var sha512) || sha512 != library.Sha512)
                {
                    // raise validation error - validate every package regardless of whether we encounter a failure.
                    if (errorMessageBuilder == null)
                    {
                        errorMessageBuilder = new StringBuilder();
                    }
                    errorMessageBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageValidationFailed, package.ToString()));
                    _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackageContentHashValidationFailed, package.ToString(), sha512, library.Sha512));
                }
            }

            if (errorMessageBuilder != null)
            {
                _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1403, errorMessageBuilder.ToString()));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluate packages.lock.json file if available and accordingly return result.
        /// </summary>
        /// <param name="packagesLockFilePath"></param>
        /// <param name="contextForProject"></param>
        /// <returns>result of packages.lock.json file evaluation where
        /// success is whether the lock file is in a valid state (ex. locked mode, but not up to date)
        /// isLockFileValid tells whether lock file is still valid to be consumed for this restore
        /// packagesLockFile is the PackagesLockFile instance
        /// </returns>
        private async Task<(bool success, bool isLockFileValid, PackagesLockFile packagesLockFile)> EvaluatePackagesLockFileAsync(
            string packagesLockFilePath,
            RemoteWalkContext contextForProject,
            TelemetryActivity lockFileTelemetry)
        {
            PackagesLockFile packagesLockFile = null;
            var isLockFileValid = false;
            var success = true;

            var restorePackagesWithLockFile = _request.Project.RestoreMetadata?.RestoreLockProperties.RestorePackagesWithLockFile;

            if (!MSBuildStringUtility.IsTrueOrEmpty(restorePackagesWithLockFile) && File.Exists(packagesLockFilePath))
            {
                success = false;

                // invalid input since packages.lock.json file exists along with RestorePackagesWithLockFile is set to false.
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidLockFileInput, packagesLockFilePath);

                // directly log to the request logger when we're not going to rewrite the assets file otherwise this log will
                // be skipped for netcore projects.
                await _request.Log.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1005, message));

                return (success, isLockFileValid, packagesLockFile);
            }

            // read packages.lock.json file if exists and RestoreForceEvaluate flag is not set to true
            if (!_request.RestoreForceEvaluate && File.Exists(packagesLockFilePath))
            {
                lockFileTelemetry.StartIntervalMeasure();
                packagesLockFile = PackagesLockFileFormat.Read(packagesLockFilePath, _logger);
                lockFileTelemetry.EndIntervalMeasure(ReadLockFileDuration);

                if (_request.DependencyGraphSpec != null)
                {
                    // check if lock file is out of sync with project data
                    lockFileTelemetry.StartIntervalMeasure();

                    LockFileValidationResult lockFileResult = PackagesLockFileUtilities.IsLockFileValid(_request.DependencyGraphSpec, packagesLockFile);
                    isLockFileValid = lockFileResult.IsValid;

                    lockFileTelemetry.EndIntervalMeasure(ValidateLockFileDuration);

                    if (isLockFileValid)
                    {
                        // pass lock file details down to generate restore graph
                        foreach (var target in packagesLockFile.Targets)
                        {
                            var libraries = target.Dependencies
                                .Where(dep => dep.Type != PackageDependencyType.Project)
                                .Select(dep => new LibraryIdentity(dep.Id, dep.ResolvedVersion, LibraryType.Package))
                                .ToList();

                            // add lock file libraries into RemoteWalkContext so that it can be used during restore graph generation
                            contextForProject.LockFileLibraries.Add(new LockFileCacheKey(target.TargetFramework, target.RuntimeIdentifier), libraries);
                        }
                    }
                    else if (_request.IsRestoreOriginalAction && _request.Project.RestoreMetadata.RestoreLockProperties.RestoreLockedMode)
                    {
                        success = false;
                        var invalidReasons = string.Join(Environment.NewLine, lockFileResult.InvalidReasons);

                        // bail restore since it's the locked mode but required to update the lock file.
                        var message = RestoreLogMessage.CreateError(NuGetLogCode.NU1004,
                                                string.Format(
                                                CultureInfo.CurrentCulture,
                                                string.Concat(invalidReasons,
                                                Strings.Error_RestoreInLockedMode)));

                        await _logger.LogAsync(message);
                    }
                }
            }

            return (success, isLockFileValid, packagesLockFile);
        }

        private (CacheFile cacheFile, bool noOp, TimeSpan? cacheFileAge) EvaluateCacheFile()
        {
            CacheFile cacheFile;
            var noOp = false;
            TimeSpan? cacheFileAge = null;

            var noOpDgSpec = NoOpRestoreUtilities.GetNoOpDgSpec(_request);

            if (_request.ProjectStyle == ProjectStyle.DotnetCliTool && _request.AllowNoOp)
            {
                // No need to attempt to resolve the tool if no-op is not allowed.
                NoOpRestoreUtilities.UpdateRequestBestMatchingToolPathsIfAvailable(_request);
            }

            var newDgSpecHash = noOpDgSpec.GetHash();

            // if --force-evaluate flag is passed then restore noop check will also be skipped.
            // this will also help us to get rid of -force flag in near future.
            // DgSpec doesn't contain log messages, so skip no-op if there are any, as it's not taken into account in the hash
            if (_request.AllowNoOp &&
                !_request.RestoreForceEvaluate &&
                CacheFileExists(_request.Project.RestoreMetadata.CacheFilePath, out cacheFileAge))
            {
                cacheFile = FileUtility.SafeRead(_request.Project.RestoreMetadata.CacheFilePath, (stream, path) => CacheFileFormat.Read(stream, _logger, path));

                if (cacheFile.IsValid && StringComparer.Ordinal.Equals(cacheFile.DgSpecHash, newDgSpecHash) && VerifyCacheFileMatchesProject(cacheFile))
                {
                    _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoreNoOpFinish, _request.Project.Name));
                    _success = true;
                    noOp = true;
                }
                else
                {
                    cacheFile = new CacheFile(newDgSpecHash);
                    _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoreNoOpDGChanged, _request.Project.Name));
                }
            }
            else
            {
                cacheFile = new CacheFile(newDgSpecHash);
            }

            // DotnetCliTool restores are special because the the assets file location is not known until after the restore itself. So we just clean up.
            if (_request.ProjectStyle == ProjectStyle.DotnetCliTool)
            {
                if (!noOp)
                {
                    // Clean up to preserve the pre no-op behavior. This should not be used, but we want to be cautious.
                    _request.LockFilePath = null;
                    _request.Project.RestoreMetadata.CacheFilePath = null;
                }
            }
            return (cacheFile, noOp, cacheFileAge);

            static bool CacheFileExists(string path, out TimeSpan? cacheFileAge)
            {
                cacheFileAge = null;

                if (path is null)
                {
                    return false;
                }

                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    cacheFileAge = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
                    return fileInfo.Exists;
                }

                return false;
            }
        }

        private string GetAssetsFilePath(LockFile lockFile)
        {
            var projectLockFilePath = _request.LockFilePath;

            if (string.IsNullOrEmpty(projectLockFilePath))
            {
                if (_request.ProjectStyle == ProjectStyle.PackageReference
                    || _request.ProjectStyle == ProjectStyle.DotnetToolReference
                    || _request.ProjectStyle == ProjectStyle.Standalone)
                {
                    projectLockFilePath = Path.Combine(_request.RestoreOutputPath, LockFileFormat.AssetsFileName);
                }
                else if (_request.ProjectStyle == ProjectStyle.DotnetCliTool)
                {
                    var toolName = ToolRestoreUtility.GetToolIdOrNullFromSpec(_request.Project);
                    var lockFileLibrary = ToolRestoreUtility.GetToolTargetLibrary(lockFile, toolName);

                    if (lockFileLibrary != null)
                    {
                        var version = lockFileLibrary.Version;

                        var toolPathResolver = new ToolPathResolver(_request.PackagesDirectory);
                        projectLockFilePath = toolPathResolver.GetLockFilePath(
                            toolName,
                            version,
                            lockFile.Targets.First().TargetFramework);
                    }
                }
                else
                {
                    projectLockFilePath = Path.Combine(_request.Project.BaseDirectory, LockFileFormat.LockFileName);
                }
            }

            return Path.GetFullPath(projectLockFilePath);
        }

        private void DowngradeLockFileIfNeeded(LockFile lockFile)
        {
            if (_request.LockFileVersion <= 1)
            {
                DowngradeLockFileToV1(lockFile);
            }
        }

        private async Task FixCaseForLegacyReaders(
            IEnumerable<RestoreTargetGraph> graphs,
            LockFile lockFile,
            CancellationToken token)
        {
            // The main restore operation restores packages with lowercase ID and version. If the
            // restore request is for lowercase packages, then take this additional post-processing
            // step.
            if (!_request.IsLowercasePackagesDirectory)
            {
                var originalCase = new OriginalCaseGlobalPackageFolder(_request, _operationId);

                // Convert the case of all the packages used in the project restore
                await originalCase.CopyPackagesToOriginalCaseAsync(graphs, token);

                // Convert the project lock file contents.
                originalCase.ConvertLockFileToOriginalCase(lockFile);
            }
        }

        private LockFile BuildAssetsFile(
            LockFile existingLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> graphs,
            IReadOnlyList<NuGetv3LocalRepository> localRepositories,
            RemoteWalkContext contextForProject)
        {
            // Build the lock file
            var lockFile = new LockFileBuilder(_request.LockFileVersion, _logger, _includeFlagGraphs)
                    .CreateLockFile(
                        existingLockFile,
                        project,
                        graphs,
                        localRepositories,
                        contextForProject,
                        _lockFileBuilderCache);

            return lockFile;
        }

        /// <summary>
        /// Check if the given graphs are valid and log errors/warnings.
        /// If fatal errors are encountered the rest of the errors/warnings
        /// are not logged. This is to avoid flooding the log with long
        /// dependency chains for every package.
        /// </summary>
        private async Task<bool> ValidateRestoreGraphsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            // Check for cycles
            var success = await ValidateCyclesAsync(graphs, logger);

            if (success)
            {
                // Check for conflicts if no cycles existed
                success = await ValidateConflictsAsync(graphs, logger);
            }

            if (success)
            {
                // Log downgrades if everything else was successful
                await LogDowngradeWarningsOrErrorsAsync(graphs, logger);
            }

            return success;
        }

        /// <summary>
        /// Logs an error and returns false if any cycles exist.
        /// </summary>
        private static async Task<bool> ValidateCyclesAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            foreach (var graph in graphs)
            {
                foreach (var cycle in graph.AnalyzeResult.Cycles)
                {
                    var text = Strings.Log_CycleDetected + $" {Environment.NewLine}  {cycle.GetPath()}.";
                    await logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1108, text, cycle.Key?.Name, graph.TargetGraphName));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Logs an error and returns false if any conflicts exist.
        /// </summary>
        private async Task<bool> ValidateConflictsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            foreach (var graph in graphs)
            {
                foreach (var versionConflict in graph.AnalyzeResult.VersionConflicts)
                {
                    var message = string.Format(
                           CultureInfo.CurrentCulture,
                           Strings.Log_VersionConflict,
                           versionConflict.Selected.Key.Name,
                           versionConflict.Selected.GetIdAndVersionOrRange(),
                           _request.Project.Name)
                       + $" {Environment.NewLine} {versionConflict.Selected.GetPathWithLastRange()} {Environment.NewLine} {versionConflict.Conflicting.GetPathWithLastRange()}.";

                    await logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1107, message, versionConflict.Selected.Key.Name, graph.TargetGraphName));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Log downgrade warnings from the graphs.
        /// </summary>
        internal static Task LogDowngradeWarningsOrErrorsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            var messages = new List<RestoreLogMessage>();

            foreach (var graph in graphs)
            {
                if (graph.AnalyzeResult.Downgrades.Count > 0)
                {
                    // Find all dependencies in the flattened graph that are not packages.
                    var ignoreIds = new HashSet<string>(
                            graph.Flattened.Where(e => e.Key.Type != LibraryType.Package)
                                       .Select(e => e.Key.Name),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var downgrade in graph.AnalyzeResult.Downgrades)
                    {
                        var downgraded = downgrade.DowngradedFrom;
                        var downgradedBy = downgrade.DowngradedTo;

                        // Filter out non-package dependencies
                        if (!ignoreIds.Contains(downgraded.Key.Name))
                        {
                            // Not all dependencies have a min version, if one does not exist use 0.0.0
                            var fromVersion = downgraded.GetVersionRange().MinVersion
                                            ?? new NuGetVersion(0, 0, 0);

                            // Use the actual version resolved if it exists
                            var toVersion = downgradedBy.GetVersionOrDefault()
                                            ?? downgradedBy.GetVersionRange().MinVersion
                                            ?? new NuGetVersion(0, 0, 0);

                            var message = string.Format(
                                    CultureInfo.CurrentCulture,
                                    downgradedBy.Item.IsCentralTransitive ? Strings.Log_CPVM_DowngradeError : Strings.Log_DowngradeWarning,
                                    downgraded.Key.Name,
                                    fromVersion,
                                    toVersion)
                                + $" {Environment.NewLine} {downgraded.GetPathWithLastRange()} {Environment.NewLine} {downgradedBy.GetPathWithLastRange()}";

                            if (downgradedBy.Item.IsCentralTransitive)
                            {
                                messages.Add(RestoreLogMessage.CreateError(NuGetLogCode.NU1109, message, downgraded.Key.Name, graph.TargetGraphName));
                            }
                            else
                            {
                                messages.Add(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1605, message, downgraded.Key.Name, graph.TargetGraphName));
                            }
                        }
                    }
                }
            }

            // Merge and log messages
            var mergedMessages = DiagnosticUtility.MergeOnTargetGraph(messages);
            return logger.LogMessagesAsync(mergedMessages);
        }

        private static async Task<IList<CompatibilityCheckResult>> VerifyCompatibilityAsync(
                PackageSpec project,
                Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs,
                IReadOnlyList<NuGetv3LocalRepository> localRepositories,
                LockFile lockFile,
                IEnumerable<RestoreTargetGraph> graphs,
                bool validateRuntimeAssets,
                ILogger logger)
        {
            // Scan every graph for compatibility, as long as there were no unresolved packages
            var checkResults = new List<CompatibilityCheckResult>();
            if (graphs.All(g => !g.Unresolved.Any()))
            {
                var checker = new CompatibilityChecker(localRepositories, lockFile, validateRuntimeAssets, logger);
                foreach (var graph in graphs)
                {
                    // Don't do compat checks for the ridless graph of DotnetTooReference restore. Everything relevant will be caught in the graph with the rid
                    if (!(ProjectStyle.DotnetToolReference == project.RestoreMetadata?.ProjectStyle && string.IsNullOrEmpty(graph.RuntimeIdentifier)))
                    {
                        var includeFlags = IncludeFlagUtils.FlattenDependencyTypes(includeFlagGraphs, project, graph);

                        var res = await checker.CheckAsync(graph, includeFlags, project);

                        checkResults.Add(res);
                        if (res.Success)
                        {
                            await logger.LogAsync(LogLevel.Verbose, string.Format(CultureInfo.CurrentCulture, Strings.Log_PackagesAndProjectsAreCompatible, graph.Name));
                        }
                        else
                        {
                            // Get error counts on a project vs package basis
                            var projectCount = res.Issues.Count(issue => issue.Type == CompatibilityIssueType.ProjectIncompatible);
                            var packageCount = res.Issues.Count(issue => issue.Type != CompatibilityIssueType.ProjectIncompatible);

                            // Log a summary with compatibility error counts
                            if (projectCount > 0)
                            {
                                await logger.LogAsync(LogLevel.Debug, $"Incompatible projects: {projectCount}");
                            }

                            if (packageCount > 0)
                            {
                                await logger.LogAsync(LogLevel.Debug, $"Incompatible packages: {packageCount}");
                            }
                        }
                    }
                    else
                    {
                        await logger.LogAsync(LogLevel.Verbose, string.Format(CultureInfo.CurrentCulture, Strings.Log_SkippingCompatibiilityCheckOnRidlessGraphForDotnetToolReferenceProject, graph.Name));
                    }
                }
            }

            return checkResults;
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteWalkContext context,
            CancellationToken token,
            TelemetryActivity telemetryActivity)
        {
            if (_request.Project.TargetFrameworks.Count == 0)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ProjectDoesNotSpecifyTargetFrameworks, _request.Project.Name, _request.Project.FilePath);
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1001, message));

                _success = false;
                return Enumerable.Empty<RestoreTargetGraph>();
            }
            _logger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, _request.Project.FilePath));

            // Get external project references
            // If the top level project already exists, update the package spec provided
            // with the RestoreRequest spec.
            var updatedExternalProjects = GetProjectReferences();

            // Load repositories
            // the external project provider is specific to the current restore project
            context.ProjectLibraryProviders.Add(
                    new PackageSpecReferenceDependencyProvider(updatedExternalProjects, _logger));

            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = _request.Project.Name,
                VersionRange = new VersionRange(_request.Project.Version),
                TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
            };

            // Resolve dependency graphs
            var allGraphs = new List<RestoreTargetGraph>();
            var runtimeIds = RequestRuntimeUtility.GetRestoreRuntimes(_request);
            var projectFrameworkRuntimePairs = CreateFrameworkRuntimePairs(_request.Project, runtimeIds);
            var hasSupports = _request.Project.RuntimeGraph.Supports.Count > 0;

            var projectRestoreRequest = new ProjectRestoreRequest(
                _request,
                _request.Project,
                _request.ExistingLockFile,
                _logger)
            {
                ParentId = _operationId
            };

            var projectRestoreCommand = new ProjectRestoreCommand(projectRestoreRequest);

            Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph> result = null;
            bool failed = false;
            using (telemetryActivity.StartIndependentInterval(CreateRestoreTargetGraphDuration))
            {
                try
                {
                    result = await projectRestoreCommand.TryRestoreAsync(
                        projectRange,
                        projectFrameworkRuntimePairs,
                        userPackageFolder,
                        fallbackPackageFolders,
                        remoteWalker,
                        context,
                        forceRuntimeGraphCreation: hasSupports,
                        token: token,
                        telemetryActivity: telemetryActivity,
                        telemetryPrefix: string.Empty);
                }
                catch (FatalProtocolException)
                {
                    failed = true;
                }
            }

            if (!failed)
            {
                var success = result.Item1;
                allGraphs.AddRange(result.Item2);
                _success = success;
            }
            else
            {
                _success = false;
                // When we fail to create the graphs, we want to write a `target` for each target framework
                // in order to avoid missing target errors from the SDK build tasks and ensure that NuGet errors don't get cleared.
                foreach (FrameworkRuntimePair frameworkRuntimePair in CreateFrameworkRuntimePairs(_request.Project, RequestRuntimeUtility.GetRestoreRuntimes(_request)))
                {
                    allGraphs.Add(RestoreTargetGraph.Create(_request.Project.RuntimeGraph, Enumerable.Empty<GraphNode<RemoteResolveResult>>(), context, _logger, frameworkRuntimePair.Framework, frameworkRuntimePair.RuntimeIdentifier));
                }
            }

            // Calculate compatibility profiles to check by merging those defined in the project with any from the command line
            foreach (var profile in _request.Project.RuntimeGraph.Supports)
            {
                var runtimes = result.Item3;

                CompatibilityProfile compatProfile;
                if (profile.Value.RestoreContexts.Any())
                {
                    // Just use the contexts from the project definition
                    compatProfile = profile.Value;
                }
                else if (!runtimes.Supports.TryGetValue(profile.Value.Name, out compatProfile))
                {
                    // No definition of this profile found, so just continue to the next one
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_UnknownCompatibilityProfile, profile.Key);

                    await _logger.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1502, message));
                    continue;
                }

                foreach (var pair in compatProfile.RestoreContexts)
                {
                    _logger.LogDebug($" {profile.Value.Name} -> +{pair}");
                    _request.CompatibilityProfiles.Add(pair);
                }
            }

            // Walk additional runtime graphs for supports checks
            if (_success && _request.CompatibilityProfiles.Any())
            {
                Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph> compatibilityResult = null;
                using (telemetryActivity.StartIndependentInterval(CreateAdditionalRestoreTargetGraphDuration))
                {
                    compatibilityResult = await projectRestoreCommand.TryRestoreAsync(
                    projectRange,
                    _request.CompatibilityProfiles,
                    userPackageFolder,
                    fallbackPackageFolders,
                    remoteWalker,
                    context,
                    forceRuntimeGraphCreation: true,
                    token: token,
                    telemetryActivity: telemetryActivity,
                    telemetryPrefix: "Additional-");
                }

                _success = compatibilityResult.Item1;

                // TryRestore may contain graphs that are already in allGraphs if the
                // supports section contains the same TxM as the project framework.
                var currentGraphs = new HashSet<KeyValuePair<NuGetFramework, string>>(
                    allGraphs.Select(graph => new KeyValuePair<NuGetFramework, string>(
                        graph.Framework,
                        graph.RuntimeIdentifier))
                    );

                foreach (var graph in compatibilityResult.Item2)
                {
                    var key = new KeyValuePair<NuGetFramework, string>(
                        graph.Framework,
                        graph.RuntimeIdentifier);

                    if (currentGraphs.Add(key))
                    {
                        allGraphs.Add(graph);
                    }
                }
            }


            return allGraphs;
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteSubProtoTypeRestoreAsync(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteWalkContext context,
            CancellationToken token,
            TelemetryActivity telemetryActivity)
        {
            if (_request.Project.TargetFrameworks.Count == 0)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ProjectDoesNotSpecifyTargetFrameworks, _request.Project.Name, _request.Project.FilePath);
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1001, message));

                _success = false;
                return Enumerable.Empty<RestoreTargetGraph>();
            }

            var localRepositories = new List<NuGetv3LocalRepository>();
            localRepositories.Add(userPackageFolder);
            localRepositories.AddRange(fallbackPackageFolders);

            _logger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, _request.Project.FilePath));

            // Get external project references
            // If the top level project already exists, update the package spec provided
            // with the RestoreRequest spec.
            var updatedExternalProjects = GetProjectReferences();

            // Load repositories
            // the external project provider is specific to the current restore project
            context.ProjectLibraryProviders.Add(
                    new PackageSpecReferenceDependencyProvider(updatedExternalProjects, _logger));

            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = _request.Project.Name,
                VersionRange = new VersionRange(_request.Project.Version),
                TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
            };

            // Resolve dependency graphs
            var allGraphs = new List<RestoreTargetGraph>();
            var graphByTFM = new Dictionary<NuGetFramework, RestoreTargetGraph>();
            var runtimeIds = RequestRuntimeUtility.GetRestoreRuntimes(_request);
            var projectFrameworkRuntimePairs = CreateFrameworkRuntimePairs(_request.Project, runtimeIds);
            int patience = 0;
            int maxOutstandingRefs = 0;
            int totalLookups = 0;
            int totalDeepLookups = 0;
            int totalEvictions = 0;
            int totalHardEvictions = 0;

            Stopwatch sw2_prototype = Stopwatch.StartNew();
            Stopwatch sw3_preamble = new Stopwatch();
            Stopwatch sw4_voScan = new Stopwatch();
            Stopwatch sw5_fullImport = new Stopwatch();
            Stopwatch sw6_flatten = new Stopwatch();

            foreach (var pair in projectFrameworkRuntimePairs)
            {
                sw3_preamble.Start();
                _logger.LogMinimal($"BSW_R1, Starting {pair.Framework},{pair.RuntimeIdentifier},{_request.Project.FilePath}");
                //A nice debug break point for analysis, works well with /p:RestoreDisableParallel=true
                //                if (_request.Project.FilePath.Contains("Microsoft.Exchange.Diagnostics"))
                //                    _logger.LogMinimal($"BSW_BP");


                //Build up our new RestoreTargetGraph.
                //This is done by making everything on RTG setable.  We should move to using a normal constructor.
                var newRTG = new RestoreTargetGraph();
                //Set the statically setable stuff
                newRTG.AnalyzeResult = new AnalyzeResult<RemoteResolveResult>();
                newRTG.Conflicts = new List<ResolverConflict>();
                newRTG.InConflict = false; //its never set fwiw...
                newRTG.Install = new HashSet<RemoteMatch>();
                newRTG.ResolvedDependencies = new HashSet<ResolvedDependencyKey>();
                newRTG.RuntimeGraph = null; //never used?
                newRTG.Unresolved = new HashSet<LibraryRange>();


                //Set the natively settable things.
                newRTG.Conventions = new Client.ManagedCodeConventions(_request.Project.RuntimeGraph);
                //This seems to match the original code, but causes sources\dev\data\src\DcDiscovery\Microsoft.Exchange.DcDiscovery.Library.csproj
                // to not process correctly.  instead using newRTG.Conventions = olderRTG.Conventions; fixes it.
                //Though, there is no a concrete difference in the data contents between the two...there is maybe a ref difference?
                //aka 246
                //newRTG.Conventions = olderRTG.Conventions;

                newRTG.Framework = pair.Framework;
                newRTG.RuntimeIdentifier = (pair.RuntimeIdentifier == "" ? null : pair.RuntimeIdentifier);
                newRTG.Name = FrameworkRuntimePair.GetName(newRTG.Framework, newRTG.RuntimeIdentifier);
                newRTG.TargetGraphName = FrameworkRuntimePair.GetTargetGraphName(newRTG.Framework, newRTG.RuntimeIdentifier);

                RestoreTargetGraph tfmNonRidGraph = null;
                RuntimeGraph runtimeGraph = null;
                if (!string.IsNullOrEmpty(pair.RuntimeIdentifier))
                {
                    // We start with the non-RID TFM graph.
                    // This is guaranteed to be computed before any graph with a RID, so we can assume this will return a value.
                    tfmNonRidGraph = graphByTFM[pair.Framework];
                    Debug.Assert(tfmNonRidGraph != null);

                    // PCL Projects with Supports have a runtime graph but no matching framework.
                    var runtimeGraphPath = _request.Project.TargetFrameworks.
                            FirstOrDefault(e => NuGetFramework.Comparer.Equals(e.FrameworkName, tfmNonRidGraph.Framework))?.RuntimeIdentifierGraphPath;

                    RuntimeGraph projectProviderRuntimeGraph = ProjectRestoreCommand.GetRuntimeGraph(runtimeGraphPath, _logger);
                    runtimeGraph = ProjectRestoreCommand.GetRuntimeGraph(tfmNonRidGraph, localRepositories, projectRuntimeGraph: projectProviderRuntimeGraph, _logger);
                }

                //Now build up our new flattened graph
                var initialProject = new LibraryDependency(new LibraryRange()
                {
                    Name = _request.Project.Name,
                    VersionRange = new VersionRange(_request.Project.Version),
                    TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
                });


                //If we find newer nodes of things while we walk, we'll evict them.
                //A subset of evictions cause a re-import.  For instance if a newer chosen node has fewer refs,
                //then we might have a dangling over-elevated ref on the old one, it'll be hard evicted.
                //The second item here is the path to root.  When we add a hard-evictee, we'll also remove anything
                //added to the eviction list that contains the evictee on their path to root.
                Queue<(LibraryDependency nextRef, string pathToNextRef, HashSet<String> suppressions, Dictionary<string, VersionRange>)> refImport =
                  new Queue<(LibraryDependency, string, HashSet<string>, Dictionary<string, VersionRange>)>();

                //using string since LibraryRange has an odd equals method.  Gotta be a cleaner way to do this.
                Dictionary<string, GraphItem<RemoteResolveResult>> allResolvedItems =
                    new Dictionary<string, GraphItem<RemoteResolveResult>>(StringComparer.OrdinalIgnoreCase);

                Dictionary<string, (LibraryDependency libRef, string pathToRef, List<(HashSet<string> currentSupressions, Dictionary<string, VersionRange>)>)> chosenResolvedItems =
                                            new Dictionary<string, (LibraryDependency, string, List<(HashSet<string> currentSupressions, Dictionary<string, VersionRange>)>)>(StringComparer.OrdinalIgnoreCase);

                Dictionary<string, string> evictions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                sw3_preamble.Stop();

                sw5_fullImport.Start();

            ProcessDeepEviction:
                patience++;

#if verboseLog
                _logger.LogMinimal($"BSW_PI1,patience={patience}");
#endif

                refImport.Clear();
                chosenResolvedItems.Clear();

#if verboseLog
                foreach (var eviction in evictions)
                    _logger.LogMinimal($"BSW_PI2,evictee={eviction.Key} path={eviction.Value}");
#endif


                refImport.Enqueue((initialProject, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, VersionRange>(StringComparer.OrdinalIgnoreCase)));
                while (refImport.Count > 0)
                {
#if verboseLog
                    _logger.LogMinimal($"BSW_DI1,refWalk.Count={refImport.Count} chosenResolvedItems={chosenResolvedItems.Count}");
#endif
                    maxOutstandingRefs = Math.Max(refImport.Count, maxOutstandingRefs);
                    (var currentRef, string pathToCurrentRef, HashSet<string> currentSupressions,
                        Dictionary<string, VersionRange> currentOverrides) = refImport.Dequeue();
                    string nameOfCurrentRef = currentRef.ToString();
                    string libraryRangeOfCurrentRef = currentRef.LibraryRange.ToString();

                    bool isProject = nameOfCurrentRef.StartsWith("project");

#if verboseLog
                    _logger.LogMinimal($"BSW_DI2, dequed ({currentRef},it={currentRef.IncludeType},sp={currentRef.SuppressParent},vo={currentRef.VersionOverride}, ptr={pathToCurrentRef})");
#endif
                    if (evictions.ContainsKey(libraryRangeOfCurrentRef))
                    {
#if verboseLog
                        _logger.LogMinimal($"BSW_DI3, Skipping {currentRef.LibraryRange} due to eviction");
#endif
                        continue;
                    }

                    if (currentOverrides.ContainsKey(currentRef.Name))
                    {
                        if (currentRef.VersionOverride != null)
                        {
                            //                                    _logger.LogMinimal($"BSW_ERR, Found override of override {currentRef} as it matches the override of {currentOverrides[currentRef.Name]}");

                        }
                        if (currentOverrides[currentRef.Name] != currentRef.LibraryRange.VersionRange)
                        {
                            if (currentRef.VersionOverride != null)
                            {
                                _logger.LogMinimal($"BSW_ERR, Found override of override {currentRef} as it doesnt match the override of {currentOverrides[currentRef.Name]}");
                            }
#if verboseLog
                            _logger.LogMinimal($"BSW_DI4, Skipping {currentRef} as it matches the override of {currentOverrides[currentRef.Name]}");
#endif
                            continue;
                        }
                    }

                    //else if we've seen this ref (but maybe not version) before check to see if we need to upgrade
                    if (chosenResolvedItems.Keys.Contains(currentRef.Name))
                    {
                        (LibraryDependency chosenRef, string pathChosenRef,
                            List<(HashSet<string> currentSupressions, Dictionary<string, VersionRange> currentOverrides)> chosenSuppressions) = chosenResolvedItems[currentRef.Name];

#if verboseLog
                        _logger.LogMinimal($"BSW_DE1, similar currentRef ({currentRef},it={currentRef.IncludeType},sp={currentRef.SuppressParent},vo={currentRef.VersionOverride})");
                        _logger.LogMinimal($"BSW_DE2, similar  chosenRef ({chosenRef},it={chosenRef.IncludeType},sp={chosenRef.SuppressParent},vo={chosenRef.VersionOverride})");
#endif


                        VersionRange nvr = currentRef.LibraryRange.VersionRange;
                        VersionRange ovr = chosenRef.LibraryRange.VersionRange;

                        //                                if(VersionRange.PreciseEquals(ovr,nvr)!=(ovr.ToString()==nvr.ToString()))
                        //                                    _logger.LogMinimal($"BSW_ERR, Found a version range that is not equal but the string is {ovr} {nvr}");

                        if (!RemoteDependencyWalker.IsGreaterThanOrEqualTo(ovr, nvr))
                        {
                            //If we think the newer thing we are looking at is better, remove the old one and let it fall thru.
#if verboseLog
                            _logger.LogMinimal($"BSW_DG1,{currentRef.LibraryRange} -> {chosenResolvedItems[currentRef.Name]}");
                            _logger.LogMinimal($"BSW_DG2,{nvr.Equals(ovr)}, {nvr.MinVersion},{nvr.MaxVersion},{ovr.MinVersion},{ovr.MaxVersion}");
                            _logger.LogMinimal($"BSW_DG3,{RemoteDependencyWalker.IsGreaterThanOrEqualTo(nvr, ovr)}, {RemoteDependencyWalker.IsGreaterThanOrEqualTo(ovr, nvr)}");
#endif
//                            if ((nvr.MinVersion.Major == 7) && (nvr.MinVersion.Minor == 3) && (currentRef.LibraryRange.ToString().Contains("Identity")))
//                                _logger.LogMinimal("BSW_BP");

                            chosenResolvedItems.Remove(currentRef.Name);
                            //Record an eviction for the node we are replacing.  The eviction path is for the current node.
                            string evictedLR = chosenRef.LibraryRange.ToString();
                            evictions.Add(evictedLR, pathToCurrentRef + " -> " + libraryRangeOfCurrentRef);

                            int deepEvictions = 0;
                            //unwind anything chosen by the node we're evicting..
                            HashSet<string> chosenItemsToRemove = null;
                            foreach (var chosenItem in chosenResolvedItems)
                            {
                                if (chosenItem.Value.pathToRef.Contains(evictedLR))
                                {
                                    if (chosenItemsToRemove == null)
                                        chosenItemsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    chosenItemsToRemove.Add(chosenItem.Key);
                                }
                            }
                            if (chosenItemsToRemove != null)
                            {
                                foreach (var chosenItemToRemove in chosenItemsToRemove)
                                {
#if verboseLog
                                    _logger.LogMinimal($"BSW_DG4, eviction lead to remove from chosen {chosenResolvedItems[chosenItemToRemove]}");
#endif
                                    chosenResolvedItems.Remove(chosenItemToRemove);
                                    deepEvictions++;
                                }
                            }
                            HashSet<string> evicteesToRemove = null;
                            foreach (var evictee in evictions)
                            {
                                if (evictee.Value.Contains(evictedLR))
                                {
                                    if (evicteesToRemove == null)
                                        evicteesToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    evicteesToRemove.Add(evictee.Key);
                                }
                            }
                            if (evicteesToRemove != null)
                            {
                                foreach (var evicteeToRemove in evicteesToRemove)
                                {
#if verboseLog
                                    _logger.LogMinimal($"BSW_DG5, eviction lead to remove from evictions {evicteeToRemove}");
#endif
                                    evictions.Remove(evicteeToRemove);
                                    deepEvictions++;
                                }
                            }
                            totalEvictions++;
                            //Since this is a "new" choice, its gets a new import context list
                            chosenResolvedItems.Add(currentRef.Name, (currentRef, pathToCurrentRef,
                                new List<(HashSet<string>, Dictionary<string, VersionRange>)> { (currentSupressions, currentOverrides) }));
                            if (deepEvictions > 0)
                            {
#if verboseLog
                                _logger.LogMinimal($"BSW_DG6, Evicted {deepEvictions} deeper nodes due to {evictedLR}");
#endif
                                totalHardEvictions++;
                                goto ProcessDeepEviction;
                            }
                            //if we are going to live with this queue and chosen state, we need to also kick
                            // any queue members who were descendants of the thing we just evicted.
                            var newRefImport =
                                new Queue<(LibraryDependency, string, HashSet<string>, Dictionary<string, VersionRange>)>();
                            while(refImport.Count>0)
                            {
                                (var cR, string pTCR, HashSet<string> cS, Dictionary<string, VersionRange> cO) =
                                    refImport.Dequeue();
#if verboseLog
                                if(pTCR.Contains(evictedLR))
                                    _logger.LogMinimal($"BSW_SCORE - {cR} - {pTCR} - {evictedLR}");
#endif
                                if (!pTCR.Contains(evictedLR))
                                    newRefImport.Enqueue((cR,pTCR,cS,cO));
                            }
                            refImport = newRefImport;
                        }
                        //if its lower we'll never do anything other than skip it.
                        //                                else if (ovr.ToString() != nvr.ToString())
                        else if (!VersionRange.PreciseEquals(ovr, nvr))
                        {
#if verboseLog
                            _logger.LogMinimal($"BSW_PE1, is lower {currentRef.LibraryRange}");
#endif
                            continue;

                        }
                        else
                        //we are looking at same.  consider if its an upgrade.
                        {
#if verboseLog
                            _logger.LogMinimal($"BSW_DU1, Maybe skipping {currentRef.LibraryRange}");
#endif
                            //If the one we already have chosen is pure, then we can skip this one.  Processing it wont bring any new info
                            if ((chosenSuppressions.Count == 1) && (chosenSuppressions[0].currentSupressions.Count == 0) &&
                                (chosenSuppressions[0].currentOverrides.Count == 0))
                            {
#if verboseLog
                                _logger.LogMinimal($"BSW_DU2, chosenSuppressions.Count == 0 {currentRef.LibraryRange}");
#endif
                                continue;
                            }
                            //if the one we are now looking at is pure, then we should replace the one we have chosen because if we're here it isnt pure.
                            else if ((currentSupressions.Count == 0) && (currentOverrides.Count == 0))
                            {
                                chosenResolvedItems.Remove(currentRef.Name);
                                //slightly evil, but works.. we should just shift to the current thing as ref?
                                chosenResolvedItems.Add(currentRef.Name, (currentRef, pathToCurrentRef,
                                new List<(HashSet<string>, Dictionary<string, VersionRange>)> { (currentSupressions, currentOverrides) }));

                            }
                            else
                            //check to see if we are equal to one of the dispositions or if we are less restrictive than one
                            {
                                bool isEqualOrSuperSetDisposition = false;
                                foreach (var chosenImportDisposition in chosenSuppressions)
                                {
                                    bool localIsEqualOrSuperSetDisposition = currentSupressions.IsSupersetOf(chosenImportDisposition.currentSupressions);

                                    bool localIsEqualorSuperSetOverride = currentOverrides.Count >= chosenImportDisposition.currentOverrides.Count;
                                    if (localIsEqualorSuperSetOverride)
                                    {
                                        foreach (var chosenOverride in chosenImportDisposition.currentOverrides)
                                        {
                                            if (!currentOverrides.ContainsKey(chosenOverride.Key))
                                            {
                                                localIsEqualorSuperSetOverride = false;
                                                break;
                                            }
                                            if (!VersionRange.PreciseEquals(currentOverrides[chosenOverride.Key], chosenOverride.Value))
                                            {
                                                localIsEqualorSuperSetOverride = false;
                                                break;
                                            }
                                        }
                                    }

                                    if (localIsEqualOrSuperSetDisposition && localIsEqualorSuperSetOverride)
                                    {
                                        isEqualOrSuperSetDisposition = true;
                                    }
                                }

                                if (isEqualOrSuperSetDisposition)
                                {
#if verboseLog
                                    _logger.LogMinimal($"BSW_DU3, Skipping {currentRef.LibraryRange} as it is more restrictive than {chosenRef.LibraryRange}");
#endif
                                    continue;
                                }
                                else
                                {
                                    //case of differently restrictive dispositions or less restrictive... we should technically be able to remove
                                    //a disposition if its less restrictive than another.  But we'll just add it to the list.
                                    chosenResolvedItems.Remove(currentRef.Name);
                                    var newImportDisposition =
                                        new List<(HashSet<string>, Dictionary<string, VersionRange>)> { (currentSupressions, currentOverrides) };
                                    newImportDisposition.AddRange(chosenSuppressions);
                                    //slightly evil, but works.. we should just shift to the current thing as ref?
                                    chosenResolvedItems.Add(currentRef.Name, (currentRef, pathToCurrentRef, newImportDisposition));
#if verboseLog
                                    _logger.LogMinimal($"BSW_DU4, Skipping {currentRef.LibraryRange} as it is more restrictive than {chosenRef.LibraryRange}");
#endif
                                    //continue;

                                }
                            }
                        }
                    }
                   else
                    {
#if verboseLog
                        _logger.LogMinimal($"BSW_MC1, Marking as Chosen ({currentRef},it={currentRef.IncludeType},sp={currentRef.SuppressParent},vo={currentRef.VersionOverride})");
#endif
                        //This is now the thing we think is the highest version of this ref
                        chosenResolvedItems.Add(currentRef.Name, (currentRef, pathToCurrentRef,
                                new List<(HashSet<string>, Dictionary<string, VersionRange>)> { (currentSupressions, currentOverrides) }));

                    }
                    GraphItem<RemoteResolveResult> refItem = null;
                    if (!allResolvedItems.TryGetValue(libraryRangeOfCurrentRef, out refItem))
                    {


                        refItem = ResolverUtility.FindLibraryCachedAsync(
                        currentRef.LibraryRange,
                                    newRTG.Framework,
                                    newRTG.RuntimeIdentifier,
                                    context,
                                    CancellationToken.None).Result;
                        totalDeepLookups++;
#if verboseLog
                        _logger.LogMinimal($"BSW_PF1, {libraryRangeOfCurrentRef}");
#endif
                        allResolvedItems.Add(libraryRangeOfCurrentRef, refItem);
                    }
#if verboseLog
                    _logger.LogMinimal($"BSW_PF2, {libraryRangeOfCurrentRef}, {allResolvedItems[libraryRangeOfCurrentRef].Key.ToString()}");
#endif
                    totalLookups++;

                    HashSet<string> suppressions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    suppressions.AddRange(currentSupressions);
                    Dictionary<string, VersionRange> overrides =
                        new Dictionary<string, VersionRange>(StringComparer.OrdinalIgnoreCase);
                    overrides.AddRange(currentOverrides);
                    //Scan for supressions and overrides
                    foreach (var dep in refItem.Data.Dependencies)
                    {
                        string depName = dep.Name;
                        if ((dep.SuppressParent == LibraryIncludeFlags.All) && (isProject == false))
                        {
                            suppressions.Add(depName);
                        }
                        if (dep.VersionOverride != null)
                        {
                            overrides[depName] = dep.VersionOverride;
                        }
                    }
                    foreach (var sup in suppressions)
                    {
#if verboseLog
                        _logger.LogMinimal($"BSW_PF3, Suppressed {sup}");
#endif
                    }
                    foreach (var ov in overrides)
                    {
#if verboseLog
                        _logger.LogMinimal($"BSW_PF4, Override {ov.Key} to {ov.Value}");
#endif
                    }
                    foreach (var dep in refItem.Data.Dependencies)
                    {
                        string depName = dep.Name;
#if verboseLog
                        //if(dep.Name.Contains("Microsoft.Cloud.InstrumentationFramework.VC14"))
                        _logger.LogMinimal($"BSW_PF5, CR={currentRef.LibraryRange},dep={dep},it={dep.IncludeType},sp={dep.SuppressParent},tfm={pair.Framework}, ptcr={pathToCurrentRef}");
#endif
                        //Suppress this node
                        if (suppressions.Contains(depName))
                        {
                            continue;
                        }
#if verboseLog
                        _logger.LogMinimal($"BSW_PF6, EnQued ({dep},it={dep.IncludeType},sp={dep.SuppressParent})");
#endif
                        refImport.Enqueue((dep, pathToCurrentRef + " -> " + libraryRangeOfCurrentRef, suppressions, overrides));
                    }

                    // Add runtime dependencies of the current node if a runtime identifier has been specified.
                    if (!string.IsNullOrEmpty(pair.RuntimeIdentifier))
                    {
                        // Check for runtime dependencies.
                        HashSet<LibraryDependency> runtimeDependencies = new HashSet<LibraryDependency>();
                        LibraryRange libraryRange = currentRef.LibraryRange;

                        RemoteDependencyWalker.EvaluateRuntimeDependencies(ref libraryRange, pair.RuntimeIdentifier, runtimeGraph, ref runtimeDependencies);

                        if (runtimeDependencies.Count > 0)
                        {
                            // If there are runtime dependencies that need to be added, remove the currentRef from allResolvedItems,
                            // and add the newly created version that contains the previously detected dependencies and newly detected runtime dependencies.
                            allResolvedItems.Remove(libraryRangeOfCurrentRef);
                            bool rootHasInnerNodes = (refItem.Data.Dependencies.Count + (runtimeDependencies == null ? 0 : runtimeDependencies.Count)) > 0;
                            GraphNode<RemoteResolveResult> rootNode = new GraphNode<RemoteResolveResult>(libraryRange, rootHasInnerNodes, false)
                            {
                                Item = refItem,
                            };
                            RemoteDependencyWalker.MergeRuntimeDependencies(runtimeDependencies, rootNode);
                            allResolvedItems.Add(libraryRangeOfCurrentRef, rootNode.Item);
                        }

                        foreach (var dep in runtimeDependencies)
                        {
                            refImport.Enqueue((dep, pathToCurrentRef + " -> " + libraryRangeOfCurrentRef, suppressions, overrides));
                        }
                    }
                }

                sw5_fullImport.Stop();
                sw6_flatten.Start();

                //Now that we've completed import, figure out the short real flattened list
                var newFlattened = new HashSet<GraphItem<RemoteResolveResult>>();
                HashSet<string> visitedItems = new HashSet<string>();
                Queue<string> itemsToFlatten = new Queue<string>();

                itemsToFlatten.Enqueue(initialProject.Name);
                while (itemsToFlatten.Count > 0)
                {
                    var currentName = itemsToFlatten.Dequeue();
                    if (visitedItems.Contains(currentName))
                        continue;
                    visitedItems.Add(currentName);
                    if (!chosenResolvedItems.ContainsKey(currentName))
                        continue;
                    (LibraryDependency chosenRef, string pathToChosenRef, var chosenSuppressions) = chosenResolvedItems[currentName];
                    LibraryRange currLib = chosenRef.LibraryRange;
                    string currLibLibrarRange = currLib.ToString();
#if verboseLog
                    _logger.LogMinimal($"BSW_DF1,{chosenRef}");
#endif
                    if (!allResolvedItems.ContainsKey(currLibLibrarRange))
                        _logger.LogMinimal($"BSW_DF2, {currLibLibrarRange}");
                    else
                    {
                        newFlattened.Add(allResolvedItems[currLibLibrarRange]);
                        foreach (var dep in allResolvedItems[currLibLibrarRange].Data.Dependencies)
                        {
                            itemsToFlatten.Enqueue(dep.Name);
                        }
                    }
                }

                sw6_flatten.Stop();

                newRTG.Flattened = newFlattened;
                //Copy Results from the original RTG
                //newRTG.Flattened = olderRTG.Flattened;


                //stuff to make graphs
                var nGraph = new List<GraphNode<RemoteResolveResult>>();
                var newGraphNode = new GraphNode<RemoteResolveResult>(initialProject.LibraryRange);
                newGraphNode.Item = allResolvedItems[initialProject.LibraryRange.ToString()];
                nGraph.Add(newGraphNode);


                //newRTG.Graphs = olderRTG.Graphs;


                newRTG.Graphs = nGraph;
                allGraphs.Add(newRTG);
                if (string.IsNullOrEmpty(pair.RuntimeIdentifier))
                {
                    graphByTFM.Add(pair.Framework, newRTG);
                }
            }

            sw2_prototype.Stop();


            _logger.LogMinimal($"BSW_R0,tp={sw2_prototype.ElapsedMilliseconds}," +
                $"tpr={sw3_preamble.ElapsedMilliseconds},tvo={sw4_voScan.ElapsedMilliseconds}," +
                $"tfi={sw5_fullImport.ElapsedMilliseconds},tf={sw6_flatten.ElapsedMilliseconds},pa={patience},mor={maxOutstandingRefs},tl={totalLookups},tdl={totalDeepLookups},te={totalEvictions},the={totalHardEvictions}");
            return allGraphs;

        }

        private List<ExternalProjectReference> GetProjectReferences()
        {
            // External references
            var updatedExternalProjects = new List<ExternalProjectReference>();

            if (_request.ExternalProjects.Count == 0)
            {
                // If no projects exist add the current project.json file to the project
                // list so that it can be resolved.
                updatedExternalProjects.Add(ToExternalProjectReference(_request.Project));
            }
            else if (_request.ExternalProjects.Count > 0)
            {
                // There should be at most one match in the external projects.
                var rootProjectMatches = _request.ExternalProjects.Where(proj =>
                        string.Equals(
                            _request.Project.Name,
                            proj.PackageSpecProjectName,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (rootProjectMatches.Count > 1)
                {
                    throw new InvalidOperationException($"Ambiguous project name '{_request.Project.Name}'.");
                }

                var rootProject = rootProjectMatches.SingleOrDefault();

                if (rootProject != null)
                {
                    // Replace the project spec with the passed in package spec,
                    // for installs which are done in memory first this will be
                    // different from the one on disk
                    updatedExternalProjects.AddRange(_request.ExternalProjects
                        .Where(project =>
                            !project.UniqueName.Equals(rootProject.UniqueName, StringComparison.Ordinal)));

                    var updatedReference = new ExternalProjectReference(
                        rootProject.UniqueName,
                        _request.Project,
                        rootProject.MSBuildProjectPath,
                        rootProject.ExternalProjectReferences);

                    updatedExternalProjects.Add(updatedReference);
                }
            }
            else
            {
                // External references were passed, but the top level project wasn't found.
                // This is always due to an internal issue and typically caused by errors
                // building the project closure.
                Debug.Fail("RestoreRequest.ExternalProjects contains references, but does not contain the top level references. Add the project we are restoring for.");
                throw new InvalidOperationException($"Missing external reference metadata for {_request.Project.Name}");
            }

            return updatedExternalProjects;
        }

        private static IEnumerable<FrameworkRuntimePair> CreateFrameworkRuntimePairs(
            PackageSpec packageSpec,
            ISet<string> runtimeIds)
        {
            var projectFrameworkRuntimePairs = new List<FrameworkRuntimePair>();
            foreach (var framework in packageSpec.TargetFrameworks)
            {
                // We care about TFM only and null RID for compilation purposes
                projectFrameworkRuntimePairs.Add(new FrameworkRuntimePair(framework.FrameworkName, null));

                foreach (var runtimeId in runtimeIds)
                {
                    projectFrameworkRuntimePairs.Add(new FrameworkRuntimePair(framework.FrameworkName, runtimeId));
                }
            }

            return projectFrameworkRuntimePairs;
        }

        private static RemoteWalkContext CreateRemoteWalkContext(RestoreRequest request, RestoreCollectorLogger logger)
        {
            var context = new RemoteWalkContext(
                request.CacheContext,
                request.PackageSourceMapping,
                logger);

            foreach (var provider in request.DependencyProviders.LocalProviders)
            {
                context.LocalLibraryProviders.Add(provider);
            }

            foreach (var provider in request.DependencyProviders.RemoteProviders)
            {
                context.RemoteLibraryProviders.Add(provider);
            }

            // Determine if the targets and props files should be written out.
            context.IsMsBuildBased = request.ProjectStyle != ProjectStyle.DotnetCliTool;

            return context;
        }

        private static void DowngradeLockFileToV1(LockFile lockFile)
        {
            // Remove projects from the library section
            var libraryProjects = lockFile.Libraries.Where(lib => lib.Type == LibraryType.Project).ToArray();

            foreach (var library in libraryProjects)
            {
                lockFile.Libraries.Remove(library);
            }

            // Remove projects from the targets section
            foreach (var target in lockFile.Targets)
            {
                var targetProjects = target.Libraries.Where(lib => lib.Type == LibraryType.Project).ToArray();

                foreach (var library in targetProjects)
                {
                    target.Libraries.Remove(library);
                }
            }

            foreach (var library in lockFile.Targets.SelectMany(target => target.Libraries))
            {
                // Null out all target types, these did not exist in v1
                library.Type = null;
            }

            // Remove the package spec
            lockFile.PackageSpec = null;
        }

        private static ExternalProjectReference ToExternalProjectReference(PackageSpec project)
        {
            return new ExternalProjectReference(
                project.Name,
                project,
                msbuildProjectPath: null,
                projectReferences: Enumerable.Empty<string>());
        }

        private static class TraceEvents
        {
            private const string EventNameBuildAssetsFile = "RestoreCommand/BuildAssetsFile";
            private const string EventNameBuildRestoreGraph = "RestoreCommand/BuildRestoreGraph";
            private const string EventNameCalcNoOpRestore = "RestoreCommand/CalcNoOpRestore";

            public static void BuildAssetsFileStart(string filePath)
            {
                var eventOptions = new EventSourceOptions
                {
                    Keywords = NuGetEventSource.Keywords.Performance |
                                NuGetEventSource.Keywords.Restore,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameBuildAssetsFile, eventOptions, new { FilePath = filePath });
            }

            public static void BuildAssetsFileStop(string filePath)
            {
                var eventOptions = new EventSourceOptions
                {
                    Keywords = NuGetEventSource.Keywords.Performance |
                                NuGetEventSource.Keywords.Restore,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameBuildAssetsFile, eventOptions, new { FilePath = filePath });
            }

            public static void BuildRestoreGraphStart(string filePath)
            {
                var eventOptions = new EventSourceOptions
                {
                    Keywords = NuGetEventSource.Keywords.Performance |
                                NuGetEventSource.Keywords.Restore,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameBuildRestoreGraph, eventOptions, new { FilePath = filePath });
            }

            public static void BuildRestoreGraphStop(string filePath)
            {
                var eventOptions = new EventSourceOptions
                {
                    Keywords = NuGetEventSource.Keywords.Performance |
                                NuGetEventSource.Keywords.Restore,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameBuildRestoreGraph, eventOptions, new { FilePath = filePath });
            }

            public static void CalcNoOpRestoreStart(string filePath)
            {
                var eventOptions = new EventSourceOptions
                {
                    Keywords = NuGetEventSource.Keywords.Performance |
                                NuGetEventSource.Keywords.Restore,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameCalcNoOpRestore, eventOptions, new { FilePath = filePath });
            }

            public static void CalcNoOpRestoreStop(string filePath)
            {
                var eventOptions = new EventSourceOptions
                {
                    Keywords = NuGetEventSource.Keywords.Performance |
                                NuGetEventSource.Keywords.Restore,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameCalcNoOpRestore, eventOptions, new { FilePath = filePath });
            }
        }
    }
}
