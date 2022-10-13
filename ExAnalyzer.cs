using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Cjk;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Ja;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtremeFind
{
    public class ExAnalyzer : StopwordAnalyzerBase
    {
        private class DefaultSetHolder
        {
            static readonly string[] STOP_WORDS =
            {
                "a", "and", "are", "as", "at", "be", "but", "by", "for", "if",
                "in", "into", "is", "it", "no", "not", "of", "on", "or", "s", "such", "t", "that",
                "the", "their", "then", "there", "these", "they", "this", "to", "was", "will",
                "with"
            };
            static readonly string[] STOP_PART_OF_SPEECH =
            {
                "接続詞", "助動詞", "助詞", "助詞-格助詞", "助詞-格助詞-一般", "助詞-格助詞-引用", "助詞-格助詞-連語",
                "助詞-接続助詞", "助詞-係助詞", "助詞-副助詞", "助詞-間投助詞", "助詞-並立助詞", "助詞-終助詞",
                "助詞-副助詞／並立助詞／終助詞", "助詞-連体化", "助詞-副詞化", "助詞-特殊"
            };
            public static CharArraySet LoadDefaultStopSet(LuceneVersion matchVersion)
            {
                return StopFilter.MakeStopSet(matchVersion, STOP_WORDS);
            }

            public static ISet<string> LoadDefaultStopTagSet()
            {
                J2N.Collections.Generic.HashSet<string> stopSet = new J2N.Collections.Generic.HashSet<string>();
                foreach(string partOfSpeech in STOP_PART_OF_SPEECH) {
                    stopSet.Add(partOfSpeech);
                }
                return stopSet;
            }
        }

        public ExAnalyzer(LuceneVersion matchVersion)
            :base(matchVersion, DefaultSetHolder.LoadDefaultStopSet(matchVersion))
        {
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            Tokenizer tokenizer = new JapaneseTokenizer(reader, null, true, JapaneseTokenizer.DEFAULT_MODE);
            TokenStream stream = new JapaneseBaseFormFilter(tokenizer);
            stream = new JapanesePartOfSpeechStopFilter(m_matchVersion, stream, DefaultSetHolder.LoadDefaultStopTagSet());
            stream = new CJKWidthFilter(stream);
            stream = new StopFilter(m_matchVersion, stream, m_stopwords);
            stream = new JapaneseKatakanaStemFilter(stream);
            return new TokenStreamComponents(tokenizer, stream);
        }
    }
}
