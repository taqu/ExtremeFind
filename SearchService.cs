using J2N.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Documents.Extensions;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Simple;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExtremeFind
{
    public struct SearchQuery
    {
        public string text_;
        public bool caseSensitive_;
    }
    public interface ISearchService
    {
        Task InitializeAsync(CancellationToken cancellationToken);
        System.Threading.Tasks.Task IndexingAsync();
        System.Threading.Tasks.Task DeleteAsync();
        System.Threading.Tasks.Task UpdateAsync();
        //System.Threading.Tasks.Task<int?> SearchAsync(SearchToolWindowControl control, SearchQuery searchQuery);
        void ClearPathCache();
    }

    public interface SSearchService
    {
    }

    public class SearchService : SSearchService, ISearchService
    {
        public const Lucene.Net.Util.LuceneVersion AppLuceneVersion = Lucene.Net.Util.LuceneVersion.LUCENE_48;

        public static long UtcNow(DateTime time)
        {
            return (long)time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static long UtcNow()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public bool InIndexing
        {
            get {
                lock(lock_) {
                    return inIndexing_;
                }
            }

            set {
                lock(lock_) {
                    inIndexing_ = value;
                }
            }
        }

        public long LastIndexUpdated
        {
            get {
                lock(lock_) {
                    return lastIndexUpdated_;
                }
            }

            set {
                lock(lock_) {
                    lastIndexUpdated_ = value;
                }
            }
        }

        public SearchService(Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider)
        {
            serviceProvider_ = serviceProvider;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await InitializeInternalAsync();
        }

        private async Task<bool> InitializeInternalAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return false;
            }
            string solutionFileName = dte2.Solution.FileName;
            if(string.IsNullOrEmpty(solutionFileName)) {
                return false;
            }
            string solutionDirectory = System.IO.Path.GetDirectoryName(solutionFileName);
            string fileInfoPath = System.IO.Path.Combine(solutionDirectory, ".vs", "esinfo.db");
            string indexPath = System.IO.Path.Combine(solutionDirectory, ".vs", "esindex");

            if(!System.IO.Directory.Exists(indexPath)) {
                try {
                    System.IO.Directory.CreateDirectory(indexPath);
                } catch {
                    return false;
                }
            }

            try {
                fileInfoDb_ = new LiteDB.LiteDatabase(fileInfoPath);
                indexDirectory_ = FSDirectory.Open(indexPath);
                analyzer_ = new Lucene.Net.Analysis.Ja.JapaneseAnalyzer(AppLuceneVersion);
            } catch(Exception e) {
                ExtremeFindPackage.Output(string.Format("ExtremeFind: Initialize {0}\n", e));
                return false;
            }
            return true;
        }

        public class PathDate
        {
            public string Path { get; set; }
            public long Date { get; set; }
        };

        private async Task<int> IndexFilesAsync(List<Tuple<string, string>> items)
        {
            int indexFileCount = 0;
            IndexWriterConfig indexWriterConfig = new IndexWriterConfig(AppLuceneVersion, analyzer_);
            indexWriterConfig.OpenMode = OpenMode.CREATE_OR_APPEND;
            LiteDB.ILiteCollection<PathDate> pathdates = fileInfoDb_.GetCollection<PathDate>("pathdate");
            pathdates.EnsureIndex(x => x.Path, unique: true);
            using(IndexWriter indexWriter = new IndexWriter(indexDirectory_, indexWriterConfig))
            using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
#if DEBUG
                Stopwatch stopwatchFiltering = Stopwatch.StartNew();
#endif
                IndexSearcher indexSearcher = new IndexSearcher(indexReader);

                List<PathDate> updateQueries = new List<PathDate>(128);
                {//Deleting
                    List<Query> deleteQueries = new List<Query>(128);
                    for(int i = 0; i < items.Count;) {
                        if(!System.IO.File.Exists(items[i].Item2)) {
                            deleteQueries.Add(new TermQuery(new Term("path", items[i].Item1)));
                            pathdates.Delete(items[i].Item1);
                            items.RemoveAt(i);
                            continue;
                        }
                        try {
                            System.IO.FileInfo fileInfo = new System.IO.FileInfo(items[i].Item2);
                            long currentLastWriteTime = UtcNow(fileInfo.LastWriteTimeUtc);

                            try {
                                string path = items[i].Item1;
                            var result = pathdates.FindOne(x=>x.Path==path);
                            if(null != result) {
                                if(result.Date == currentLastWriteTime) {
                                    items.RemoveAt(i);
                                    continue;
                                }
                            }
                            }catch(Exception e) {
                                ExtremeFindPackage.Output(string.Format("ExtremeFind: find {0}\n", e));
                            }
                            deleteQueries.Add(new TermQuery(new Term("path", items[i].Item1)));
                            updateQueries.Add(new PathDate { Path = items[i].Item1, Date = currentLastWriteTime });
                            ++i;
                        } catch {
                            items.RemoveAt(i);
                        }
                    } //for(int i = start;
                    if(0 < deleteQueries.Count) {
                        indexWriter.DeleteDocuments(deleteQueries.ToArray());
                    }
                }
#if DEBUG
                stopwatchFiltering.Stop();
                ExtremeFindPackage.Output(string.Format("ExtremeFind: filtering files {0} milliseconds\n", stopwatchFiltering.ElapsedMilliseconds));
#endif
                List<Lucene.Net.Documents.Document> documents = new List<Lucene.Net.Documents.Document>(1024);
                int fileCount = 0;
                for(int i = 0; i < items.Count; ++i) {
                    Tuple<string, string> file = items[i];
                    try {
                        int lineCount = 0;
                        using(StreamReader streamReader = new StreamReader(file.Item2)) {
                            while(0 <= streamReader.Peek()) {
                                string line = await streamReader.ReadLineAsync();
                                Lucene.Net.Documents.Document document = new Lucene.Net.Documents.Document();
                                Lucene.Net.Documents.StringField pathField = new Lucene.Net.Documents.StringField("path", file.Item1, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(pathField);
                                Lucene.Net.Documents.Field lineField = new Lucene.Net.Documents.Int32Field("line", lineCount, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(lineField);
                                Lucene.Net.Documents.Field contents = new Lucene.Net.Documents.TextField("contents", line, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(contents);
                                documents.Add(document);

                                ++lineCount;
                            }
                            if(64 <= ++fileCount) {
                                fileCount = 0;
                                indexWriter.AddDocuments(documents);
                                documents.Clear();
                            }
                            await Task.Delay(0);
                            ++indexFileCount;
                        }
                        //ExtremeFindPackage.Output(string.Format("ExtremeFind: index {0} {1} {2}\n", file.Item1, file.Item2, lineCount));
                    } catch(Exception e) {
                        ExtremeFindPackage.Output(string.Format("ExtremeFind: Indexing {0}\n", e));
                    }
                } //for(int i = start; i < end; ++i)
                try {
                    if(0 < documents.Count) {
                        indexWriter.AddDocuments(documents);
                        documents.Clear();
                    }
                } catch {

                }
                if(0 < updateQueries.Count) {
                    foreach(PathDate pathDate in updateQueries) {
                        pathdates.Upsert(pathDate);
                    }
                }
            }
            return indexFileCount;
        }

        public async Task IndexingAsync()
        {
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return;
            }
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if(null == dte2.Solution.Projects) {
                return;
            }

            ExtremeFindPackage package = await serviceProvider_.GetServiceAsync(typeof(ExtremeFindPackage)) as ExtremeFindPackage;
            if(null == package) {
                return;
            }
            string solutionPath = dte2.Solution.FileName;
            if(string.IsNullOrEmpty(solutionPath)) {
                return;
            }
#if DEBUG
            Stopwatch stopwatch = Stopwatch.StartNew();
#endif
            OptionExtremeFind dialog = package.GetDialogPage(typeof(OptionExtremeFind)) as OptionExtremeFind;
            HashSet<string> extensionSet = dialog.ExtensionSet;

#if DEBUG
            Stopwatch stopwatchFileGather = Stopwatch.StartNew();
#endif
            List<Tuple<string, string>> items = new List<Tuple<string, string>>(1024);
            string path = System.IO.Path.GetFileNameWithoutExtension(solutionPath);
            foreach(EnvDTE.Project project in dte2.Solution.Projects) {
                string projectRoot = System.IO.Path.Combine(path, project.Name);
                IndexingTraverse(items, projectRoot, project.ProjectItems, extensionSet);
            }
#if DEBUG
            stopwatchFileGather.Stop();
            ExtremeFindPackage.Output(string.Format("ExtremeFind: indexing gathering file {0} in {1} milliseconds\n", items.Count, stopwatchFileGather.ElapsedMilliseconds));
#endif
            //if(items.Count <= 256) {
            int indexFileCount = await IndexFilesAsync(items);
            //} else {
            //    int bulk = (int)Math.Ceiling((double)items.Count / maxThreads);
            //    if(0 == bulk) {
            //        bulk = 1;
            //    }
            //    Task[] tasks = new Task[bulk];
            //    bulk = items.Count / bulk;
            //    int count = 0;
            //    for(int i = 0; i < items.Count; i += bulk) {
            //        int start = i;
            //        int end = Math.Min(i + bulk, items.Count);
            //        tasks[count] = IndexFilesAsync(items, start, end, extensionSet);
            //        ++count;
            //    }
            //    await Task.WhenAll(tasks);
            //}//if(items.Count
#if DEBUG
            stopwatch.Stop();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            try {
                using(IndexWriter indexWriter = new IndexWriter(indexDirectory_, new IndexWriterConfig(AppLuceneVersion, analyzer_)))
                using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
                    IndexSearcher indexSearcher = new IndexSearcher(indexReader);
                    CollectionStatistics stats = indexSearcher.CollectionStatistics("contents");
                    ExtremeFindPackage.Output(string.Format("ExtremeFind: indexing {0}/{1} in {2} milliseconds, {3} docs in db\n", indexFileCount, items.Count, elapsedMilliseconds, stats.DocCount));
                }
            } catch(Exception e) {
                ExtremeFindPackage.Output(string.Format("ExtremeFind: indexing {0}\n", e));
            }
#endif
        }

        private void IndexingTraverse(List<Tuple<string, string>> items, string path, EnvDTE.ProjectItems projectItems, HashSet<string> extensionSet)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            foreach(EnvDTE.ProjectItem projectItem in projectItems) {
                switch(projectItem.Kind) {
                case EnvDTE.Constants.vsProjectItemKindPhysicalFile:
                    if(0 < projectItem.FileCount) {
                        string filepath = projectItem.FileNames[0];
                        string extension = System.IO.Path.GetExtension(filepath);
                        if(!string.IsNullOrEmpty(extension) && 0 < extension.Length && '.' == extension[0]) {
                            extension = extension.Substring(1, extension.Length - 1);
                        }
                        if(extensionSet.Contains(extension)) {
                            items.Add(new Tuple<string, string>(System.IO.Path.Combine(path, projectItem.Name), filepath));
                        }
                    }
                    break;
                case EnvDTE.Constants.vsProjectItemKindPhysicalFolder: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindVirtualFolder: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindSolutionItems: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.SubProject && null != projectItem.SubProject.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.SubProject.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindSubProject: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.SubProject && null != projectItem.SubProject.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.SubProject.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindMisc:
                    break;
                }
            }
        }

        public async Task DeleteAsync()
        {
            if(null == indexDirectory_) {
                bool init = await InitializeInternalAsync();
                if(!init) {
                    return;
                }
            }
            ExtremeFindPackage package = await serviceProvider_.GetServiceAsync(typeof(ExtremeFindPackage)) as ExtremeFindPackage;
            if(null == package) {
                return;
            }
            OptionExtremeFind dialog = package.GetDialogPage(typeof(OptionExtremeFind)) as OptionExtremeFind;
            long timePreUpdate = Math.Max(0, UtcNow() - dialog.IndexExpiryTime);
            IndexWriterConfig indexWriterConfig = new IndexWriterConfig(AppLuceneVersion, analyzer_);

            try {
                using(IndexWriter indexWriter = new IndexWriter(indexDirectory_, indexWriterConfig))
                using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
                    IndexSearcher indexSearcher = new IndexSearcher(indexReader);
                    Query query = NumericRangeQuery.NewInt64Range("update", 0, timePreUpdate, true, true);
                    indexWriter.DeleteDocuments(query);
#if DEBUG
                    CollectionStatistics statsAfter = indexSearcher.CollectionStatistics("contents");
                    ExtremeFindPackage.Output(string.Format("ExtremeFind: Documents after deleting {0}\n", statsAfter.DocCount));
#endif
                }
            } catch(Exception e) {
                ExtremeFindPackage.Output(string.Format("ExtremeFind: Deleting {0}\n", e));
            }
        }

        private void GetProjectPaths(HashSet<string> items)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return;
            }
            if(null == dte2.Solution.Projects) {
                return;
            }
            string path = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FileName);
            foreach(EnvDTE.Project project in dte2.Solution.Projects) {
                GetProjectPathsTraverse(items, path, project);
            }
        }

        private void GetProjectPathsTraverse(HashSet<string> items, string path, EnvDTE.Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            switch(project.Kind) {
            case ProjectTypes.ProjectFolders:
            case ProjectTypes.SolutionFolder:
                path = System.IO.Path.Combine(path, project.Name);
                break;
            default:
                path = System.IO.Path.Combine(path, project.Name);
                items.Add(path);
                return;
            }
            if(null != project.Collection) {
                foreach(EnvDTE.Project child in project.Collection) {
                    GetProjectPathsTraverse(items, path, child);
                }
            }

            if(null != project.ProjectItems) {
                foreach(EnvDTE.ProjectItem projectItem in project.ProjectItems) {
                    GetProjectPathsTraverse(items, path, projectItem);
                }
            }
        }

        private void GetProjectPathsTraverse(HashSet<string> items, string path, EnvDTE.ProjectItem projectItem)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            switch(projectItem.Kind) {
            case EnvDTE.Constants.vsProjectItemKindPhysicalFolder:
            case EnvDTE.Constants.vsProjectItemKindVirtualFolder:
                path = System.IO.Path.Combine(path, projectItem.Name);
                break;
            case EnvDTE.Constants.vsProjectItemKindSolutionItems:
                if(null != projectItem.ProjectItems) {
                    path = System.IO.Path.Combine(path, projectItem.Name);
                    items.Add(path);
                } else if(null != projectItem.Collection) {
                    path = System.IO.Path.Combine(path, projectItem.Name);
                    items.Add(path);
                } else {
                    return;
                }
                break;
            case EnvDTE.Constants.vsProjectItemKindSubProject:
                path = System.IO.Path.Combine(path, projectItem.Name);
                items.Add(path);
                break;
            default:
                break;
            }
            EnvDTE.ProjectItems projectItems = projectItem.ProjectItems;
            if(null == projectItems) {
                return;
            }
            projectItems = projectItem.Collection;
            if(null == projectItems) {
                return;
            }
            foreach(EnvDTE.ProjectItem child in projectItems) {
                GetProjectPathsTraverse(items, path, child);
            }
        }

        public async Task UpdateAsync()
        {
            if(null == indexDirectory_) {
                bool init = await InitializeInternalAsync();
                if(!init) {
                    return;
                }
            }
            ExtremeFindPackage package = await serviceProvider_.GetServiceAsync(typeof(ExtremeFindPackage)) as ExtremeFindPackage;
            if(null == package) {
                return;
            }
            OptionExtremeFind dialog = package.GetDialogPage(typeof(OptionExtremeFind)) as OptionExtremeFind;
            int updateMinInterval = dialog.UpdateMinInterval;
            long now = UtcNow();
            lock(lock_) {
                if(inIndexing_) {
                    return;
                }
                long lastUpdate = lastIndexUpdated_ + updateMinInterval;
                if(now < lastUpdate) {
                    return;
                }
                inIndexing_ = true;
            }
            await DeleteAsync();
            await IndexingAsync();
            now = UtcNow();
            lock(lock_) {
                lastIndexUpdated_ = now;
                inIndexing_ = false;
            }
        }

        #if false
        public async Task<int?> SearchAsync(SearchToolWindowControl control, SearchQuery searchQuery)
        {
            RebuildPachCache();

            if(null == indexDirectory_) {
                bool init = await InitializeInternalAsync();
                if(!init) {
                    return null;
                }
            }
            ExtremeFindPackage package = await serviceProvider_.GetServiceAsync(typeof(ExtremeFindPackage)) as ExtremeFindPackage;
            if(null == package) {
                return null;
            }
            OptionExtremeFind dialog = package.GetDialogPage(typeof(OptionExtremeFind)) as OptionExtremeFind;
            int maxSearchItems = dialog.MaxSearchItems;

#if DEBUG
            Stopwatch stopwatch = Stopwatch.StartNew();
#endif
            int numResults = 0;
            try {
                //control.SearchResults.Clear();
                using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
                    IndexSearcher indexSearcher = new IndexSearcher(indexReader);
                    SimpleQueryParser simpleQueryParser = new SimpleQueryParser(analyzer_, "contents");
                    Query query = simpleQueryParser.Parse(searchQuery.text_);
                    TopDocs result = indexSearcher.Search(query, maxSearchItems);
                    if(null == result || null == result.ScoreDocs) {
                        return numResults;
                    }
                    numResults = result.ScoreDocs.Length;
                    int count = 0;
                    foreach(ScoreDoc scoreDoc in result.ScoreDocs) {
                        Lucene.Net.Documents.Document doc = indexSearcher.Doc(scoreDoc.Doc);
                        //control.SearchResults.Add(new SearchResult {
                        //    ProjectPath = doc.GetField("path").GetStringValue(),
                        //    Content = doc.GetField("contents").GetStringValue(),
                        //    Line = doc.GetField("line").GetInt32ValueOrDefault()
                        //});
                        if(1024 <= ++count) {
                            await Task.Delay(0);
                        }
                    }
                }
            } catch(Exception e) {
                ExtremeFindPackage.Output(string.Format("ExtremeFind: Searching {0}\n", e));
            }
#if DEBUG
            stopwatch.Stop();
            ExtremeFindPackage.Output(string.Format("ExtremeFind: Searching {0} milliseconds\n", stopwatch.ElapsedMilliseconds));
#endif
            return numResults;
        }
        #endif

        public void ClearPathCache()
        {
            pathCache_ = null;
        }

        private void RebuildPachCache()
        {
            if(null == pathCache_) {
                pathCache_ = new HashSet<string>(64);
                GetProjectPaths(pathCache_);
            }
        }


        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider_;
        private LiteDB.LiteDatabase fileInfoDb_;
        private FSDirectory indexDirectory_;
        private Analyzer analyzer_;
        private HashSet<string> pathCache_;

        private object lock_ = new object();
        private bool inIndexing_ = false;
        private long lastIndexUpdated_;
    }
}

