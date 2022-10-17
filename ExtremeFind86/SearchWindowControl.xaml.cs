using EnvDTE;
using EnvDTE80;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace ExtremeFind86
{
    /// <summary>
    /// Interaction logic for SearchWindowControl.
    /// </summary>
    public partial class SearchWindowControl : UserControl
    {
        public enum SearchMethod
        {
            Simple =0,
            Fuzzy,
        };

        public class SearchResult
        {
            public string Path { get; set; }
            public string Content { get; set; }
            public int Line { get; set; }
        }

        public ObservableCollection<SearchResult> Results
        {
            get { return results_; }
        }

        public string TextStatus
        {
            private get { return TextBlockStatus.Text; }
            set {
                TextBlockStatus.Text = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchWindowControl"/> class.
        /// </summary>
        public SearchWindowControl()
        {
            this.InitializeComponent();
            ListViewResult.ItemsSource = results_;
        }

        private void OnClickButtonSearch(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(TextBoxSearch.Text)) {
                return;
            }
            ExtremeFind86Package package = null;
            if(!ExtremeFind86Package.Package.TryGetTarget(out package)) {
                return;
            }
            SearchQuery query = new SearchQuery { text_ = TextBoxSearch.Text, caseSensitive_ = (bool)CheckBoxCaseSensitive.IsChecked};
            package.JoinableTaskFactory.Run(
                async () => {
                    ISearchService service = await package.GetServiceAsync(typeof(SSearchService)) as ISearchService;
                    if(null != service) {
                        await service.SearchAsync(this, query);
                    }
                }
            );
        }

        private void OnKeyDownTextSearch(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == System.Windows.Input.Key.Return) {
                OnClickButtonSearch(null, null);
            }
        }

        private void OnMouseDoubleClickListViewResult(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            SearchResult item = ListViewResult.SelectedItem as SearchResult;
            if(null == item) {
                return;
            }
            ExtremeFind86Package package = null;
            if(!ExtremeFind86Package.Package.TryGetTarget(out package)) {
                return;
            }
            string realPath = SearchService.GetRealPath(item.Path);
            if(string.IsNullOrEmpty(realPath) || !System.IO.File.Exists(realPath)) {
                return;
            }
            DTE2 dte2 = ExtremeFind86Package.GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
            if(null == dte2) {
                return;
            }
            OptionExtremeFind dialog = package.GetDialogPage(typeof(OptionExtremeFind)) as OptionExtremeFind;
            EnvDTE.Window window = dte2.ItemOperations.OpenFile(realPath);
            if(null == window) {
                return;
            }
            (window.Document.Selection as TextSelection)?.GotoLine(item.Line + 1, dialog.SelectLineWhenJumping);
        }

        private ObservableCollection<SearchResult> results_ = new ObservableCollection<SearchResult>();
    }
}