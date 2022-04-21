﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Community.VisualStudio.Toolkit
{
    /// <summary>
    /// Represents a project in the solution hierarchy.
    /// </summary>
    public class Project : SolutionItem
    {
        private ReferenceCollection? _references;

        internal Project(IVsHierarchyItem item, SolutionItemType type) : base(item, type)
        { ThreadHelper.ThrowIfNotOnUIThread(); }

        /// <summary>
        /// Starts a build, rebuild, or clean of the project.
        /// </summary>
        public Task<bool> BuildAsync(BuildAction action = BuildAction.Build)
        {
            return VS.Build.BuildProjectAsync(this, action);
        }

        /// <summary>
        /// Adds one or more files to the project.
        /// </summary>
        public async Task<IEnumerable<PhysicalFile>> AddExistingFilesAsync(params string[] filePaths)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            GetItemInfo(out IVsHierarchy hierarchy, out uint itemId, out _);

            VSADDRESULT[] result = new VSADDRESULT[filePaths.Count()];
            IVsProject ip = (IVsProject)hierarchy;

            ErrorHandler.ThrowOnFailure(ip.AddItem(itemId, VSADDITEMOPERATION.VSADDITEMOP_LINKTOFILE, string.Empty, (uint)filePaths.Count(), filePaths, IntPtr.Zero, result));

            List<PhysicalFile> files = new();

            foreach (string filePath in filePaths)
            {
                PhysicalFile? file = await PhysicalFile.FromFileAsync(filePath);

                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        /// <summary>
        /// References in the project.
        /// </summary>
        public ReferenceCollection References => _references ??= new(this);

        /// <summary>
        /// Checks what kind the project is.
        /// </summary>
        /// <param name="typeGuid">Use the <see cref="ProjectTypes"/> collection for known GUIDs.</param>
        public async Task<bool> IsKindAsync(string typeGuid)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            return hierarchy.IsProjectOfType(typeGuid);
        }

        /// <summary>
        /// Tests if the given capability is found in the project's capabilities.
        /// </summary>
        public bool IsCapabilityMatch(string capability)
        {
            GetItemInfo(out IVsHierarchy? hier, out _, out _);
            return hier.IsCapabilityMatch(capability);
        }

        /// <summary>
        /// Tries to remove an attribute in the project file for the item.
        /// </summary>
        public Task<bool> RemoveAttributeAsync(string name)
        {
            return RemoveAttributeAsync(name, ProjectStorageType.ProjectFile);
        }

        /// <summary>
        /// Tries to remove an attribute in the project file for the item from the specified storage type.
        /// </summary>
        public async Task<bool> RemoveAttributeAsync(string name, ProjectStorageType storageType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            if (hierarchy is IVsBuildPropertyStorage storage)
            {
                storage.RemoveProperty(name, "", ToPersistStorageType(storageType));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Save the project if it's dirty.
        /// </summary>
        public async Task SaveAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            IVsSolution solution = await VS.Services.GetSolutionAsync();
            int hr = solution.SaveSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_SaveIfDirty, hierarchy, 0);

            ErrorHandler.ThrowOnFailure(hr);
        }

        /// <summary>
        /// Tries to set an attribute in the project file for the item.
        /// </summary>
        public Task<bool> TrySetAttributeAsync(string name, string value)
        {
            return TrySetAttributeAsync(name, value, ProjectStorageType.ProjectFile);
        }

        /// <summary>
        /// Tries to set an attribute in the project file for the item, storing the value in the specified storage type.
        /// </summary>
        public async Task<bool> TrySetAttributeAsync(string name, string value, ProjectStorageType storageType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            if (hierarchy is IVsBuildPropertyStorage storage)
            {
                storage.SetPropertyValue(name, "", ToPersistStorageType(storageType), value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to retrieve an attribute value from the project file for the item.
        /// </summary>
        /// <returns><see langword="null"/> if the attribute doesn't exist.</returns>
        public Task<string?> GetAttributeAsync(string name)
        {
            return GetAttributeAsync(name, ProjectStorageType.ProjectFile);
        }

        /// <summary>
        /// Tries to retrieve an attribute value from the project file for the item, getting the value from the specfied storage type.
        /// </summary>
        /// <returns><see langword="null"/> if the attribute doesn't exist.</returns>
        public async Task<string?> GetAttributeAsync(string name, ProjectStorageType storageType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            if (hierarchy is IVsBuildPropertyStorage storage)
            {
                storage.GetPropertyValue(name, "", ToPersistStorageType(storageType), out string? value);
                return value;
            }

            return null;
        }

        private static uint ToPersistStorageType(ProjectStorageType type)
        {
            return (uint)(type switch
            {
                ProjectStorageType.UserFile => _PersistStorageType.PST_USER_FILE,
                _ => _PersistStorageType.PST_PROJECT_FILE
            });
        }

        /// <summary>
        /// Determines whether the project is loaded.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                GetItemInfo(out _, out _, out IVsHierarchyItem item);
                return !HierarchyUtilities.IsStubHierarchy(item.HierarchyIdentity);
            }
        }

        /// <summary>
        /// Loads the project.
        /// </summary>
        public async Task LoadAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            IVsSolution solution = await VS.Services.GetSolutionAsync();
            ErrorHandler.ThrowOnFailure(solution.GetGuidOfProject(hierarchy, out Guid guid));
            ErrorHandler.ThrowOnFailure(((IVsSolution4)solution).ReloadProject(ref guid));

            // Loading and unloading a project causes the hierarchy to be disposed,
            // so we need to refresh the underlying hierarchy object.
            await RefreshAsync(solution, guid);
        }

        /// <summary>
        /// Unloads the project.
        /// </summary>
        public async Task UnloadAsync(_VSProjectUnloadStatus reason = _VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            GetItemInfo(out IVsHierarchy hierarchy, out _, out _);

            IVsSolution solution = await VS.Services.GetSolutionAsync();
            ErrorHandler.ThrowOnFailure(solution.GetGuidOfProject(hierarchy, out Guid guid));
            ErrorHandler.ThrowOnFailure(((IVsSolution4)solution).UnloadProject(ref guid, (uint)reason));

            // Loading and unloading a project causes the hierarchy to be disposed,
            // so we need to refresh the underlying hierarchy object.
            await RefreshAsync(solution, guid);
        }

        private async Task RefreshAsync(IVsSolution solution, Guid guid)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Use the GUID to get the new IVsHierarchy object.
            ErrorHandler.ThrowOnFailure(solution.GetProjectOfGuid(ref guid, out IVsHierarchy hierarchy));

            // Use the existing item ID to get the new `IVsHierarchyItem`.
            GetItemInfo(out _, out uint itemId, out _);
            Update(await hierarchy.ToHierarchyItemAsync(itemId));
        }
    }

    /// <summary>
    /// Defines the type of file that project data is stored in.
    /// </summary>
    public enum ProjectStorageType
    {
        /// <summary>
        /// The <c>.csproj</c> file (or <c>.vbproj</c>, etc.)
        /// </summary>
        ProjectFile,
        /// <summary>
        /// The <c>.csproj.user</c> file (or <c>.vbproj.user</c>, etc.)
        /// </summary>
        UserFile
    }
}
