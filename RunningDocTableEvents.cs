﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO.Packaging;
using System.Linq;

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
            ThreadHelper.ThrowIfNotOnUIThread();
            ExtremeFindPackage package = null;
            if(!ExtremeFindPackage.Package.TryGetTarget(out package)) {
                return VSConstants.S_OK;
            }
            DTE2 dte2 = ExtremeFindPackage.GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
            if(null == dte2) {
                return VSConstants.S_OK;
            }
            RunningDocumentInfo runningDocumentInfo = runningDocumentTable_.GetDocumentInfo(docCookie);
            EnvDTE.Document document = null;
            foreach(EnvDTE.Document doc in dte2.Documents.OfType<EnvDTE.Document>())
            {
                if(doc.FullName == runningDocumentInfo.Moniker)
                {
                    document = doc;
                    break;
                }
            }
            if(null == document) {
                return VSConstants.S_OK;
            }
            if(document.Kind != EnvDTE.Constants.vsDocumentKindText
                && document.Kind != EnvDTE.Constants.vsDocumentKindHTML) {
                return VSConstants.S_OK;
            }
            ProjectItem projectItem = document.ProjectItem;
            package.JoinableTaskFactory.Run(async () => {
                ISearchService service = await package.GetServiceAsync(typeof(SSearchService)) as ISearchService;
                if(null != service && null != projectItem) {
                    await service.UpdateAsync(projectItem);
                }
            });
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
