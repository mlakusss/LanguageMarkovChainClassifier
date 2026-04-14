using System;
using System.Collections.Generic;
using System.Linq;

namespace kursachm
{
    /// <summary>
    /// Классификатор языка на основе марковской цепи порядка k с механизмом back-off.
    /// Поддерживает обучение на размеченных документах, фильтрацию по алфавиту,
    /// пониженный вес цитат и возврат вероятности для топ-5 кандидатов.
    /// </summary>
    public class MarkovChainBackoffClassifier
    {
        private readonly int maxOrder;
        private readonly double quoteWeight;
        private readonly double fallbackProb;
        private readonly TextPreprocessor preprocessor;
        private MarkovModelData model;

        /// <summary>
        /// Инициализирует новый экземпляр классификатора с заданными параметрами.
        /// </summary>
        /// <param name="maxOrder">Максимальная длина контекста (порядок цепи). По умолчанию 6.</param>
        /// <param name="quoteWeight">Вес цитат (основной текст имеет вес 1.0). По умолчанию 0.05.</param>
        /// <param name="fallbackProb">Вероятность возврата при отсутствии перехода. По умолчанию 1e-9.</param>
        public MarkovChainBackoffClassifier(int maxOrder = 6, double quoteWeight = 0.05, double fallbackProb = 1e-9)
        {
            this.maxOrder = maxOrder;
            this.quoteWeight = quoteWeight;
            this.fallbackProb = fallbackProb;
            this.preprocessor = new TextPreprocessor(maxOrder, quoteWeight);
            this.model = new MarkovModelData();
        }

        /// <summary>
        /// Обучает модель на коллекции документов. Очищает предыдущее состояние модели.
        /// </summary>
        /// <param name="documents">Коллекция документов с текстом и меткой языка.</param>
        public void Train(IEnumerable<LanguageDocument> documents)
        {
            model.Clear();
            foreach (var doc in documents)
            {
                string lang = doc.Language;
                if (!model.LanguageDocCount.ContainsKey(lang))
                {
                    model.LanguageDocCount[lang] = 0;
                    model.Transitions[lang] = new Dictionary<string, Dictionary<char, double>>();
                    model.ContextTotals[lang] = new Dictionary<string, double>();
                    model.LanguageAlphabets[lang] = new HashSet<char>();
                }
                model.LanguageDocCount[lang]++;

                var fragments = preprocessor.SplitQuotes(doc.Text).ToList();
                foreach (var (fragment, weight) in fragments)
                {
                    // Сбор алфавита языка
                    foreach (char c in fragment)
                        if (char.IsLetter(c))
                            model.LanguageAlphabets[lang].Add(char.ToLowerInvariant(c));

                    string prepared = preprocessor.PrepareText(fragment);
                    if (prepared.Length <= maxOrder) continue;

                    for (int i = maxOrder; i < prepared.Length; i++)
                    {
                        string context = prepared.Substring(i - maxOrder, maxOrder);
                        char nextChar = prepared[i];

                        if (!model.Transitions[lang].ContainsKey(context))
                        {
                            model.Transitions[lang][context] = new Dictionary<char, double>();
                            model.ContextTotals[lang][context] = 0;
                        }
                        if (!model.Transitions[lang][context].ContainsKey(nextChar))
                            model.Transitions[lang][context][nextChar] = 0;
                        model.Transitions[lang][context][nextChar] += weight;
                        model.ContextTotals[lang][context] += weight;
                    }
                }
            }
        }

        /// <summary>
        /// Вычисляет вероятность перехода от контекста к символу с использованием back-off.
        /// </summary>
        /// <param name="lang">Язык, для которого выполняется оценка.</param>
        /// <param name="fullContext">Полный контекст (строка, из которой будут браться последние символы).</param>
        /// <param name="next">Следующий символ.</param>
        /// <returns>Вероятность перехода (не меньше fallbackProb).</returns>
        private double GetProbability(string lang, string fullContext, char next)
        {
            for (int order = maxOrder; order >= 1; order--)
            {
                if (fullContext.Length < order) continue;
                string context = fullContext.Substring(fullContext.Length - order, order);
                if (model.Transitions[lang].ContainsKey(context))
                {
                    var dict = model.Transitions[lang][context];
                    if (dict.ContainsKey(next))
                    {
                        double total = model.ContextTotals[lang][context];
                        return dict[next] / total;
                    }
                }
            }
            return fallbackProb;
        }

        /// <summary>
        /// Вычисляет логарифм вероятности того, что текст порождён указанным языком.
        /// </summary>
        /// <param name="text">Анализируемый текст.</param>
        /// <param name="lang">Язык.</param>
        /// <returns>Сумма взвешенных логарифмов вероятностей переходов.</returns>
        private double LogProbability(string text, string lang)
        {
            double logProbSum = 0.0;
            var fragments = preprocessor.SplitQuotes(text).ToList();
            foreach (var (fragment, weight) in fragments)
            {
                string prep = preprocessor.PrepareText(fragment);
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
        /// Фильтрует список языков-кандидатов по пересечению их алфавита с буквами текста.
        /// </summary>
        /// <param name="text">Текст, из которого извлекаются буквы.</param>
        /// <param name="candidates">Исходный список языков.</param>
        /// <returns>Отфильтрованный список (если пуст, возвращает исходный).</returns>
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
                if (!model.LanguageAlphabets.ContainsKey(lang)) continue;
                if (model.LanguageAlphabets[lang].Overlaps(textLetters))
                    result.Add(lang);
            }
            return result.Count > 0 ? result : candidates;
        }

        /// <summary>
        /// Определяет язык текста, возвращает лучший язык, уверенность и топ-5 кандидатов.
        /// </summary>
        /// <param name="text">Текст для классификации.</param>
        /// <returns>
        /// Кортеж: (BestLanguage, Confidence, TopCandidates), где TopCandidates — список из 5 элементов
        /// (язык, логарифмическая оценка, вероятность).
        /// </returns>
        public (string BestLanguage, double Confidence, List<(string Lang, double LogProb, double Prob)> TopCandidates) Classify(string text)
        {
            if (model.LanguageDocCount.Count == 0) return (null, 0, null);

            int totalDocs = model.LanguageDocCount.Values.Sum();
            var candidates = FilterLanguagesByAlphabet(text, model.LanguageDocCount.Keys);
            if (!candidates.Any()) candidates = model.LanguageDocCount.Keys;

            var scores = new Dictionary<string, double>();
            foreach (var lang in candidates)
            {
                double prior = Math.Log((double)model.LanguageDocCount[lang] / totalDocs);
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

        /// <summary>
        /// Возвращает перечисление языков, на которых обучен классификатор.
        /// </summary>
        public IEnumerable<string> GetLanguages() => model.LanguageDocCount.Keys;
    }
}