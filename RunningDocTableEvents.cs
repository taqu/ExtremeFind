using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ExtremeFind
{
    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        public RunningDocTableEvents(AsyncPackage package)
        {
            runningDocumentTable_ = new RunningDocumentTable(package);
            runningDocumentTable_.Advise(this);
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            //ExtremeFindPackage package;
            //ExtremeFindPackage.Package.TryGetTarget(out package);
            //if(null != package) {
            //    package.JoinableTaskFactory.Run(async () => {
            //        ISearchService service = await package.GetServiceAsync(typeof(SSearchService)) as ISearchService;
            //        if(null != service) {
            //            await service.UpdateAsync();
            //        }
            //    });

            //}
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }
        private RunningDocumentTable runningDocumentTable_;
    }
}
