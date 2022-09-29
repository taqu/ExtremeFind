using Microsoft.VisualStudio.Shell.Interop;

namespace ExtremeFind
{
    public class ObjectSearchService : IVsObjectSearch
    {
        public ObjectSearchService(IVsObjectSearch baseService)
        {
            baseService_ = baseService;
        }

        public int Find(uint flags, VSOBSEARCHCRITERIA[] pobSrch, out IVsObjectList pplist)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return baseService_.Find(flags, pobSrch, out pplist);
        }

        private IVsObjectSearch baseService_;
    }
}

