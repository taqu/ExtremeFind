using J2N.Collections.Generic;
using System;
using System.ComponentModel;

namespace ExtremeFind
{
    public class OptionExtremeFind : Microsoft.VisualStudio.Shell.DialogPage
    {
        [Category("General")]
        [DisplayName("Extensions")]
        [Description("Extensions to index")]
        [DefaultValue(true)]
        public string Extensions
        {
            get { return extensions_; }
            set { extensions_ = value; extensionSet_ = null;}
        }

        [Category("General")]
        [DisplayName("Max Search Items")]
        [Description("Max search items at a time")]
        [DefaultValue(true)]
        public int MaxSearchItems
        {
            get { return maxSearchItems_; }
            set { maxSearchItems_ = Math.Max(10, Math.Min(100000, value));}
        }

        [Category("General")]
        [DisplayName("Index Expiry Time")]
        [Description("Index expiry time in seconds")]
        [DefaultValue(true)]
        public int IndexExpiryTime
        {
            get { return indexExpiryTime_; }
            set { indexExpiryTime_ = Math.Max(60*60, value);}
        }

        [Category("General")]
        [DisplayName("Index Update Minimal Interval")]
        [Description("Index update minimal interval in seconds. The index will not be updated in this interval.")]
        [DefaultValue(true)]
        public int UpdateMinInterval
        {
            get { return updateMinInterval_; }
            set { updateMinInterval_ = Math.Max(60, value);}
        }

        [Category("General")]
        [DisplayName("Select Line When Jumping")]
        [Description("Select the line, when jumping")]
        [DefaultValue(false)]
        public bool SelectLineWhenJumping
        {
            get { return selectLineWhenJumping_; }
            set { selectLineWhenJumping_ = value;}
        }

        [Category("Debug")]
        [DisplayName("Debug Log")]
        [Description("Output debug logs")]
        [DefaultValue(false)]
        public bool OutputDebugLog
        {
            get { return outputDebugLog_; }
            set { outputDebugLog_ = value;}
        }

        public HashSet<string> ExtensionSet
        {
            get
            {
                if(null == extensionSet_) {
                    Rebuild();
                }
                return extensionSet_;
            }
        }

        private void Rebuild()
        {
            extensionSet_ = new HashSet<string>(128);
            string[] tokens = extensions_.Split(' ', ',', '|', '　', '\t', ':');
            if(null == tokens) {
                return;
            }
            foreach(string token in tokens) {
                if(string.IsNullOrEmpty(token)) {
                    continue;
                }
                extensionSet_.Add(token);
            }
        }

        private string extensions_ =
                "alg as asm asp awk bas bat c cfg cgi cmd cpp css cs cxx csv clj coffee def dic dlg exp f for go h hpp hs lhs htm html inf ini inl java js latex log lsp lua sh tcl tex text txt xml xsl php vs pl ps ps1 py rb ush usm hlsl glsl";
        private HashSet<string> extensionSet_;
        private int maxSearchItems_ = 1000;
        private int indexExpiryTime_ = 60*60*24*30;
        private int updateMinInterval_ = 60;
        private bool selectLineWhenJumping_ = false;
        private bool outputDebugLog_ = false;
    }
}
