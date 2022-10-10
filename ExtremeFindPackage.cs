using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    [ProvideService(typeof(SSearchService), IsAsyncQueryable = true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ExtremeFindPackage.PackageGuidString)]
    [ProvideOptionPage(typeof(OptionExtremeFind), "ExtremeFind", "General", 0, 0, true)]
    public sealed class ExtremeFindPackage : AsyncPackage
    {
        /// <summary>
        /// ExtremeFindPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "e21aef7d-b9a5-4703-80fd-566a2a7d848a";

        #region Package Members

        public static WeakReference<ExtremeFindPackage> Package { get =>package_; }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            package_ = new WeakReference<ExtremeFindPackage>(this);
            runningDocTableEvents_ = new RunningDocTableEvents(this);

            DTE2 dte2 = GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
            solutionEvents_ = dte2.Events.SolutionEvents;
            solutionEvents_.Opened += OnSolutionOpened;

            projectItemsEvents_ = dte2.Events.SolutionItemsEvents;
            projectItemsEvents_.ItemAdded += OnProjectItemChanged;
            projectItemsEvents_.ItemRemoved += OnProjectItemChanged;
            projectItemsEvents_.ItemRenamed += OnProjectItemRenamed;

            AddService(typeof(SSearchService), CreateSearchServiceAsync);

            ISearchService service = await GetServiceAsync(typeof(SSearchService)) as ISearchService;
            if(null != service) {
                await service.UpdateAsync();
            }
        }

        private void OnSolutionOpened()
        {
        }

        private void OnProjectItemChanged(ProjectItem projectItem)
        {
        }

        private void OnProjectItemRenamed(ProjectItem projectItem, string oldName)
        {

        }

        private async Task<object> CreateSearchServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            if(typeof(SSearchService) != serviceType) {
                return null;
            }
            SearchService service = new SearchService(this);
            await service.InitializeAsync(cancellationToken);
            return service;
        }
        #endregion

        /// <summary>
        /// Print a message to the editor's output
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Output(string message)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            DTE2 dte2 = GetGlobalService(typeof(DTE)) as DTE2;
            EnvDTE.OutputWindow outputWindow = dte2.ToolWindows.OutputWindow;
            if(null == outputWindow) {
                return;
            }
            foreach(EnvDTE.OutputWindowPane window in outputWindow.OutputWindowPanes) {
                window.OutputString(message);
            }
            Trace.Write(message);
        }

        /// <summary>
        /// Print a message to the editor's output
        /// </summary>
        public static async Task OutputAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DTE2 dte2 = GetGlobalService(typeof(DTE)) as DTE2;
            EnvDTE.OutputWindow outputWindow = dte2.ToolWindows.OutputWindow;
            if(null == outputWindow) {
                return;
            }
            foreach(EnvDTE.OutputWindowPane window in outputWindow.OutputWindowPanes) {
                window.OutputString(message);
            }
            Trace.Write(message);
        }

        static private WeakReference<ExtremeFindPackage> package_;
        private SolutionEvents solutionEvents_;
        private ProjectItemsEvents projectItemsEvents_;
        private RunningDocTableEvents runningDocTableEvents_;
    }
}
