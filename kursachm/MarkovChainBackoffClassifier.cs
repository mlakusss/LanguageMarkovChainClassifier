using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace kursachm
{
    /// <summary>
    /// Классификатор языка на основе марковской цепи порядка k с back-off (без сглаживания).
    /// Для каждого языка хранятся переходы для контекстов максимальной длины.
    /// При отсутствии перехода контекст усекается слева (back-off) до длины 1.
    /// Алфавитная фильтрация: учитываются только языки, чей алфавит пересекается с буквами текста.
    /// Цитаты имеют пониженный вес.
    /// </summary>
    public class MarkovChainBackoffClassifier
    {
        private readonly int maxOrder;
        private readonly double quoteWeight;
        private readonly double fallbackProb;

        // Для каждого языка: контекст -> (следующий символ -> взвешенная частота)
        private Dictionary<string, Dictionary<string, Dictionary<char, double>>> transitions;
        private Dictionary<string, Dictionary<string, double>> contextTotals;
        private Dictionary<string, int> languageDocCount;
        private Dictionary<string, HashSet<char>> languageAlphabets;

        public MarkovChainBackoffClassifier(int maxOrder = 6, double quoteWeight = 0.05, double fallbackProb = 1e-9)
        {
            this.maxOrder = maxOrder;
            this.quoteWeight = quoteWeight;
            this.fallbackProb = fallbackProb;
            transitions = new Dictionary<string, Dictionary<string, Dictionary<char, double>>>();
            contextTotals = new Dictionary<string, Dictionary<string, double>>();
            languageDocCount = new Dictionary<string, int>();
            languageAlphabets = new Dictionary<string, HashSet<char>>();
        }

        /// <summary> 
        /// Разбивает текст на основной текст (вес 1) и цитаты (вес quoteWeight)
        /// </summary>
        private IEnumerable<(string fragment, double weight)> SplitQuotes(string text)
        {
            var regex = new Regex(@"«([^»]*)»|""([^""]*)""|“([^”]*)”", RegexOptions.Singleline);
            int lastIndex = 0;
            foreach (Match match in regex.Matches(text))
            {
                if (match.Index > lastIndex)
                {
                    string before = text.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(before))
                        yield return (before, 1.0);
                }
                string quote = null;
                if (match.Groups[1].Success) quote = match.Groups[1].Value;
                else if (match.Groups[2].Success) quote = match.Groups[2].Value;
                else if (match.Groups[3].Success) quote = match.Groups[3].Value;
                if (!string.IsNullOrWhiteSpace(quote))
                    yield return (quote, quoteWeight);
                lastIndex = match.Index + match.Length;
            }
            if (lastIndex < text.Length)
            {
                string after = text.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(after))
                    yield return (after, 1.0);
            }
        }

        /// <summary> 
        /// Очистка текста для n-грамм: только буквы и пробелы, нижний регистр 
        /// </summary>
        private string CleanTextForNGrams(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetter(c) || char.IsWhiteSpace(c))
                    sb.Append(c);
                else
                    sb.Append(' ');
            }
            string result = sb.ToString();
            result = Regex.Replace(result, @"\s+", " ").Trim().ToLowerInvariant();
            return result;
        }

        /// <summary> 
        /// Добавляет маркеры начала # для марковской цепи 
        /// </summary>
        private string PrepareText(string text)
        {
            string cleaned = CleanTextForNGrams(text);
            if (string.IsNullOrEmpty(cleaned)) return "";
            return new string('#', maxOrder) + cleaned;
        }

        /// <summary> 
        /// Обучение модели на коллекции документов 
        /// </summary>
        public void Train(IEnumerable<LanguageDocument> documents)
        {
            transitions.Clear();
            contextTotals.Clear();
            languageDocCount.Clear();
            languageAlphabets.Clear();

            foreach (var doc in documents)
            {
                string lang = doc.Language;
                if (!languageDocCount.ContainsKey(lang))
                {
                    languageDocCount[lang] = 0;
                    transitions[lang] = new Dictionary<string, Dictionary<char, double>>();
                    contextTotals[lang] = new Dictionary<string, double>();
                    languageAlphabets[lang] = new HashSet<char>();
                }
                languageDocCount[lang]++;

                var fragments = SplitQuotes(doc.Text).ToList();
                foreach (var (fragment, weight) in fragments)
                {
                    // Сбор алфавита языка (из оригинального текста)
                    foreach (char c in fragment)
                        if (char.IsLetter(c))
                            languageAlphabets[lang].Add(char.ToLowerInvariant(c));

                    string prepared = PrepareText(fragment);
                    if (prepared.Length <= maxOrder) continue;

                    for (int i = maxOrder; i < prepared.Length; i++)
                    {
                        string context = prepared.Substring(i - maxOrder, maxOrder);
                        char nextChar = prepared[i];

                        if (!transitions[lang].ContainsKey(context))
                        {
                            transitions[lang][context] = new Dictionary<char, double>();
                            contextTotals[lang][context] = 0;
                        }
                        if (!transitions[lang][context].ContainsKey(nextChar))
                            transitions[lang][context][nextChar] = 0;
                        transitions[lang][context][nextChar] += weight;
                        contextTotals[lang][context] += weight;
                    }
                }
            }
        }

        /// <summary> 
        /// Вероятность перехода с back-off (усечение контекста) 
        /// </summary>
        private double GetProbability(string lang, string fullContext, char next)
        {
            for (int order = maxOrder; order >= 1; order--)
            {
                if (fullContext.Length < order) continue;
                string context = fullContext.Substring(fullContext.Length - order, order);
                if (transitions[lang].ContainsKey(context))
                {
                    var dict = transitions[lang][context];
                    if (dict.ContainsKey(next))
                    {
                        double total = contextTotals[lang][context];
                        return dict[next] / total;
                    }
                }
            }
            return fallbackProb;
        }

        /// <summary> 
        /// Суммарная логарифмическая вероятность текста для языка 
        /// </summary>
        private double LogProbability(string text, string lang)
        {
            double logProbSum = 0.0;
            var fragments = SplitQuotes(text).ToList();
            foreach (var (fragment, weight) in fragments)
            {
                string prep = PrepareText(fragment);
                if (prep.Length <= maxOrder) continue;
                for (int i = maxOrder; i < prep.Length; i++)
                {
                    string context = prep.Substring(i - maxOrder, maxOrder);
                    char next = prep[i];
                    double p = GetProbability(lang, context, next);
                    if (p <= 0) p = fallbackProb;
                    logProbSum += weight * Math.Log(p);
                }
            }
            return logProbSum;
        }

        /// <summary> 
        /// Мягкая фильтрация языков по алфавиту (пересечение множеств букв) 
        /// </summary>
        private IEnumerable<string> FilterLanguagesByAlphabet(string text, IEnumerable<string> candidates)
        {
            var textLetters = new HashSet<char>();
            foreach (char c in text)
                if (char.IsLetter(c))
                    textLetters.Add(char.ToLowerInvariant(c));

            if (textLetters.Count == 0) return candidates;

            var result = new List<string>();
            foreach (var lang in candidates)
            {
                if (!languageAlphabets.ContainsKey(lang)) continue;
                if (languageAlphabets[lang].Overlaps(textLetters))
                    result.Add(lang);
            }
            return result.Count > 0 ? result : candidates;
        }

        /// <summary> 
        /// Классификация текста: возвращает лучший язык, уверенность и топ‑5 
        /// </summary>
        public (string BestLanguage, double Confidence, List<(string Lang, double LogProb, double Prob)> TopCandidates) Classify(string text)
        {
            if (languageDocCount.Count == 0) return (null, 0, null);

            int totalDocs = languageDocCount.Values.Sum();
            var candidates = FilterLanguagesByAlphabet(text, languageDocCount.Keys);
            if (!candidates.Any()) candidates = languageDocCount.Keys;

            var scores = new Dictionary<string, double>();
            foreach (var lang in candidates)
            {
                double prior = Math.Log((double)languageDocCount[lang] / totalDocs);
                double likelihood = LogProbability(text, lang);
                scores[lang] = prior + likelihood;
            }

            var topCandidates = scores
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .Select(kv => (Lang: kv.Key, LogProb: kv.Value, Prob: 0.0))
                .ToList();

            double maxScore = topCandidates.First().LogProb;
            double sumExp = 0;
            foreach (var t in topCandidates)
                sumExp += Math.Exp(t.LogProb - maxScore);

            var topWithProb = topCandidates
                .Select(t => (t.Lang, t.LogProb, Prob: Math.Exp(t.LogProb - maxScore) / sumExp))
                .ToList();

            return (topWithProb.First().Lang, topWithProb.First().Prob, topWithProb);
        }

        public IEnumerable<string> GetLanguages() => languageDocCount.Keys;
    }
}