using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace kursachm
{
    /// <summary>
    /// Выполняет предобработку текста для марковской цепи:
    /// разбиение на цитаты, очистку, нормализацию, добавление маркеров начала.
    /// </summary>
    public class TextPreprocessor
    {
        private readonly int maxOrder;
        private readonly double quoteWeight;

        /// <summary>
        /// Инициализирует предпроцессор с заданными параметрами.
        /// </summary>
        /// <param name="maxOrder">Максимальная длина контекста (для добавления маркеров #).</param>
        /// <param name="quoteWeight">Вес цитат (основной текст имеет вес 1.0).</param>
        public TextPreprocessor(int maxOrder, double quoteWeight)
        {
            this.maxOrder = maxOrder;
            this.quoteWeight = quoteWeight;
        }

        /// <summary>
        /// Разбивает текст на фрагменты: основной текст (вес 1.0) и цитаты (вес quoteWeight).
        /// Цитаты распознаются в кавычках «...», "..." или “...”.
        /// </summary>
        /// <param name="text">Исходный текст.</param>
        /// <returns>Последовательность кортежей (фрагмент, вес).</returns>
        public IEnumerable<(string fragment, double weight)> SplitQuotes(string text)
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
        /// Очищает текст для n-грамм: оставляет только буквы и пробелы,
        /// заменяет прочие символы на пробелы, сжимает множественные пробелы,
        /// приводит к нижнему регистру.
        /// </summary>
        /// <param name="text">Входной текст.</param>
        /// <returns>Очищенная и нормализованная строка.</returns>
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
        /// Подготавливает текст для марковской цепи: очищает, нормализует
        /// и добавляет в начало maxOrder символов '#'.
        /// </summary>
        /// <param name="text">Входной текст.</param>
        /// <returns>Подготовленная строка (или пустая строка, если текст не содержит букв).</returns>
        public string PrepareText(string text)
        {
            string cleaned = CleanTextForNGrams(text);
            if (string.IsNullOrEmpty(cleaned)) return "";
            return new string('#', maxOrder) + cleaned;
        }
    }
}