using System.Collections.Generic;

namespace kursachm
{
    /// <summary>
    /// Хранилище данных обученной модели марковской цепи.
    /// Содержит словари переходов, суммы по контекстам, счётчики документов и алфавиты языков.
    /// </summary>
    public class MarkovModelData
    {
        /// <summary>
        /// Для каждого языка: контекст → следующий символ → взвешенная частота.
        /// </summary>
        public Dictionary<string, Dictionary<string, Dictionary<char, double>>> Transitions { get; set; }

        /// <summary>
        /// Для каждого языка и контекста: суммарный вес всех переходов из этого контекста.
        /// </summary>
        public Dictionary<string, Dictionary<string, double>> ContextTotals { get; set; }

        /// <summary>
        /// Количество документов, загруженных для каждого языка.
        /// </summary>
        public Dictionary<string, int> LanguageDocCount { get; set; }

        /// <summary>
        /// Множество букв (алфавит), встретившихся в обучающих текстах языка.
        /// Используется для быстрой фильтрации кандидатов.
        /// </summary>
        public Dictionary<string, HashSet<char>> LanguageAlphabets { get; set; }

        /// <summary>
        /// Инициализирует пустую модель данных.
        /// </summary>
        public MarkovModelData()
        {
            Transitions = new Dictionary<string, Dictionary<string, Dictionary<char, double>>>();
            ContextTotals = new Dictionary<string, Dictionary<string, double>>();
            LanguageDocCount = new Dictionary<string, int>();
            LanguageAlphabets = new Dictionary<string, HashSet<char>>();
        }

        /// <summary>
        /// Очищает все словари модели.
        /// </summary>
        public void Clear()
        {
            Transitions.Clear();
            ContextTotals.Clear();
            LanguageDocCount.Clear();
            LanguageAlphabets.Clear();
        }
    }
}