// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IVsProjectAdapterProvider))]
    internal class VsProjectAdapterProvider : IVsProjectAdapterProvider
    {
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<SVsSolution> _vsSolution;

        [ImportingConstructor]
        public VsProjectAdapterProvider(
            [Import(typeof(SAsyncServiceProvider))]
            IAsyncServiceProvider serviceProvider,
            IVsProjectThreadingService threadingService)
            : this(
                  threadingService,
                  new AsyncLazy<SVsSolution>(() => serviceProvider.GetServiceAsync<SVsSolution, SVsSolution>(throwOnFailure: false), threadingService.JoinableTaskFactory))
        {
        }

        internal VsProjectAdapterProvider(
            IVsProjectThreadingService threadingService,
            AsyncLazy<SVsSolution> vsSolution)
        {
            Assumes.Present(threadingService);
            Assumes.Present(vsSolution);

            _threadingService = threadingService;
            _vsSolution = vsSolution;
        }

        public IVsProjectAdapter CreateAdapterForFullyLoadedProject(EnvDTE.Project dteProject)
        {
            return _threadingService.JoinableTaskFactory.Run(
                () => CreateAdapterForFullyLoadedProjectAsync(dteProject));
        }

        public async Task<IVsProjectAdapter> CreateAdapterForFullyLoadedProjectAsync(EnvDTE.Project dteProject)
        {
            Assumes.Present(dteProject);

            // Get services while we might be on background thread
            var vsSolution = await _vsSolution.GetValueAsync();

            // switch to main thread and use services we know must be done on main thread.
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsHierarchyItem = await VsHierarchyItem.FromDteProjectAsync(dteProject);
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject = _ => dteProject;

            var buildStorageProperty = vsHierarchyItem.VsHierarchy as IVsBuildPropertyStorage;
            var vsBuildProperties = new VsProjectBuildProperties(
                dteProject, buildStorageProperty, _threadingService);

            var projectNames = await ProjectNames.FromDTEProjectAsync(dteProject, vsSolution);
            var fullProjectPath = dteProject.GetFullProjectPath();
            if (fullProjectPath == null && dteProject.IsWebSite())
            {
                var projectDirectory = await dteProject.GetFullPathAsync();

                return new VsProjectAdapter(
                    vsHierarchyItem,
                    projectNames,
                    fullProjectPath,
                    projectDirectory,
                    loadDteProject,
                    vsBuildProperties,
                    _threadingService);
            }

            return new VsProjectAdapter(
                vsHierarchyItem,
                projectNames,
                fullProjectPath,
                loadDteProject,
                vsBuildProperties,
                _threadingService);
        }

        public async Task<IVsProjectAdapter> CreateAdapterForFullyLoadedProjectAsync(IVsHierarchy hierarchy)
        {
            Assumes.Present(hierarchy);

            // Get services while we might be on background thread
            var vsSolution = await _vsSolution.GetValueAsync();

            // switch to main thread and use services we know must be done on main thread.
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsHierarchyItem = VsHierarchyItem.FromVsHierarchy(hierarchy);
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject = hierarchy => VsHierarchyUtility.GetProjectFromHierarchy(hierarchy);

            var buildStorageProperty = vsHierarchyItem.VsHierarchy as IVsBuildPropertyStorage;
            var vsBuildProperties = new VsProjectBuildProperties(
                new Lazy<EnvDTE.Project>(() => loadDteProject(hierarchy)), buildStorageProperty, _threadingService);

            var fullProjectPath = VsHierarchyUtility.GetProjectPath(hierarchy);
            var projectNames = await ProjectNames.FromIVsSolution2(fullProjectPath, (IVsSolution2)vsSolution, hierarchy, CancellationToken.None);

            return new VsProjectAdapter(
                vsHierarchyItem,
                projectNames,
                fullProjectPath,
                loadDteProject,
                vsBuildProperties,
                _threadingService);
        }
    }
}
