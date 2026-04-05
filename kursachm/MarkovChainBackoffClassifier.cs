using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace kursachm
{
    /// <summary>
    /// Классификатор языка на основе марковской цепи порядка k с back-off (без сглаживания).
    /// Если переход для контекста длины k отсутствует, используется контекст длины k-1 и т.д.
    /// </summary>
    public class MarkovChainBackoffClassifier
    {
        private readonly int maxOrder;
        private readonly double quoteWeight;
        private readonly double fallbackProb; // вероятность для полностью неизвестного символа

        // Структура: для каждого языка и для каждого порядка (1..maxOrder) храним:
        //   - для контекста (строка длины order) -> словарь (следующий символ -> взвешенная частота)
        //   - общую сумму частот для контекста
        private Dictionary<string, Dictionary<int, Dictionary<string, Dictionary<char, double>>>> transitions;
        private Dictionary<string, Dictionary<int, Dictionary<string, double>>> contextTotals;
        private Dictionary<string, int> languageDocCount;
        private HashSet<char> globalAlphabet; // все символы, встреченные в обучении (для fallback)

        public MarkovChainBackoffClassifier(int maxOrder = 4, double quoteWeight = 0.05, double fallbackProb = 1e-9)
        {
            this.maxOrder = maxOrder;
            this.quoteWeight = quoteWeight;
            this.fallbackProb = fallbackProb;
            transitions = new Dictionary<string, Dictionary<int, Dictionary<string, Dictionary<char, double>>>>();
            contextTotals = new Dictionary<string, Dictionary<int, Dictionary<string, double>>>();
            languageDocCount = new Dictionary<string, int>();
            globalAlphabet = new HashSet<char>();
        }

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

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = Regex.Replace(text, @"[^a-zA-Zа-яА-Я\s]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim().ToLowerInvariant();
            return cleaned;
        }

        // Подготовка текста: добавление маркеров начала (символ '#') порядка maxOrder
        private string PrepareText(string text)
        {
            string cleaned = CleanText(text);
            if (string.IsNullOrEmpty(cleaned)) return "";
            return new string('#', maxOrder) + cleaned;
        }

        public void Train(IEnumerable<LanguageDocument> documents)
        {
            transitions.Clear();
            contextTotals.Clear();
            languageDocCount.Clear();
            globalAlphabet.Clear();

            foreach (var doc in documents)
            {
                string lang = doc.Language;
                if (!languageDocCount.ContainsKey(lang))
                {
                    languageDocCount[lang] = 0;
                    transitions[lang] = new Dictionary<int, Dictionary<string, Dictionary<char, double>>>();
                    contextTotals[lang] = new Dictionary<int, Dictionary<string, double>>();
                    for (int order = 1; order <= maxOrder; order++)
                    {
                        transitions[lang][order] = new Dictionary<string, Dictionary<char, double>>();
                        contextTotals[lang][order] = new Dictionary<string, double>();
                    }
                }
                languageDocCount[lang]++;

                var fragments = SplitQuotes(doc.Text).ToList();
                foreach (var (fragment, weight) in fragments)
                {
                    string prepared = PrepareText(fragment);
                    if (prepared.Length <= maxOrder) continue;

                    // Для каждого порядка (от 1 до maxOrder) собираем статистику переходов
                    // Но эффективнее: сначала собрать для maxOrder, а потом для меньших порядков использовать те же данные?
                    // Поступим проще: для каждой позиции i (от maxOrder до длины-1) извлекаем все суффиксы контекстов.
                    for (int i = maxOrder; i < prepared.Length; i++)
                    {
                        char nextChar = prepared[i];
                        globalAlphabet.Add(nextChar);

                        // Для каждого порядка от 1 до maxOrder
                        for (int order = 1; order <= maxOrder; order++)
                        {
                            // Контекст: order символов, заканчивающихся на позиции i-1
                            string context = prepared.Substring(i - order, order);
                            var transDict = transitions[lang][order];
                            var totalDict = contextTotals[lang][order];

                            if (!transDict.ContainsKey(context))
                            {
                                transDict[context] = new Dictionary<char, double>();
                                totalDict[context] = 0;
                            }
                            if (!transDict[context].ContainsKey(nextChar))
                                transDict[context][nextChar] = 0;
                            transDict[context][nextChar] += weight;
                            totalDict[context] += weight;
                        }
                    }
                }
            }
        }

        // Получение вероятности P(next | context) с back-off
        private double GetProbability(string lang, string context, char next, int order)
        {
            // Пытаемся найти переход на текущем порядке
            if (transitions[lang].ContainsKey(order) && transitions[lang][order].ContainsKey(context))
            {
                var dict = transitions[lang][order][context];
                if (dict.ContainsKey(next))
                {
                    double total = contextTotals[lang][order][context];
                    return dict[next] / total;
                }
            }
            // Если не нашли и порядок > 1, рекурсивно пробуем укороченный контекст
            if (order > 1)
            {
                string shorterContext = context.Substring(1); // убираем первый символ
                return GetProbability(lang, shorterContext, next, order - 1);
            }
            // Если дошли до униграммы (order=1) и её нет — возвращаем очень маленькую вероятность
            // Можно также использовать глобальную частоту символа, но для простоты — fallbackProb
            return fallbackProb;
        }

        // Логарифмическая вероятность текста для языка
        private double LogProbability(string text, string lang)
        {
            string prepared = PrepareText(text);
            if (prepared.Length <= maxOrder) return double.NegativeInfinity;

            double logProb = 0.0;
            var fragments = SplitQuotes(text).ToList(); // нужно для весов, но PrepareText уже сделал очистку
            // Упростим: будем считать веса для каждого фрагмента отдельно
            foreach (var (fragment, weight) in fragments)
            {
                string prep = PrepareText(fragment);
                if (prep.Length <= maxOrder) continue;
                for (int i = maxOrder; i < prep.Length; i++)
                {
                    string context = prep.Substring(i - maxOrder, maxOrder);
                    char next = prep[i];
                    double p = GetProbability(lang, context, next, maxOrder);
                    if (p <= 0) p = fallbackProb;
                    logProb += weight * Math.Log(p);
                }
            }
            return logProb;
        }

        public (string BestLanguage, double Confidence, List<(string Lang, double LogProb, double Prob)> TopCandidates) Classify(string text)
        {
            if (languageDocCount.Count == 0) return (null, 0, null);

            int totalDocs = languageDocCount.Values.Sum();
            var logScores = new Dictionary<string, double>();

            foreach (var lang in languageDocCount.Keys)
            {
                double prior = Math.Log((double)languageDocCount[lang] / totalDocs);
                double likelihood = LogProbability(text, lang);
                logScores[lang] = prior + likelihood;
            }

            var topCandidates = logScores
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