using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ExtremeFind
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ExtremeFindPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(SearchToolWindow))]
    [ProvideService(typeof(IVsObjectSearch))]
    public sealed class ExtremeFindPackage : AsyncPackage
    {
        /// <summary>
        /// ExtremeFindPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "f30975f3-7867-42a0-8f42-d6473157251b";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await SearchToolWindowCommand.InitializeAsync(this);

            ServiceCreatorCallback callback =new ServiceCreatorCallback(CreateObjectSearchService);
    ((IServiceContainer)this).AddService(typeof(ObjectSearchService), callback);

            DTE2 dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
            string version = dte2.Version;

            EnvDTE.Solution solution = dte2.Solution;
            if(null != solution) {
                foreach(EnvDTE.Project project in solution.Projects) {
                    foreach(ProjectItem projectItem in project.ProjectItems) {
                        output(projectItem.Name, dte2);
                    }
                }
            }
            IVsObjectSearch searchService = Package.GetGlobalService(typeof(IVsObjectSearch)) as IVsObjectSearch;
            if(null != searchService) {
                output(searchService.ToString(), dte2);
            }
        }

        #endregion

        private object CreateObjectSearchService(IServiceContainer container, Type serviceType)
        {
            if(typeof(ObjectSearchService) != serviceType) {
                return null;
            }
            DTE2 dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if(null == dte2) {
                return null;
            }
            IVsObjectSearch searchService = Package.GetGlobalService(typeof(IVsObjectSearch)) as IVsObjectSearch;
            if(null == searchService) {
                return null;
            }
            return new ObjectSearchService(searchService);
        }

        /**
         * @brief Print a message to the editor's output
         */
        [System.Diagnostics.Conditional("DEBUG")]
        public static void output(string message, DTE2 dte)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.OutputWindow outputWindow = dte.ToolWindows.OutputWindow;
            if(null == outputWindow) {
                return;
            }
            foreach(EnvDTE.OutputWindowPane window in outputWindow.OutputWindowPanes) {
                window.OutputString(message);
            }
        }
    }
}
