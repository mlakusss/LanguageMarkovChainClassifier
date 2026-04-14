using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kursachm
{
    /// <summary>
    /// Представляет документ для обучения: текст и метка языка.
    /// </summary>
    public class LanguageDocument
    {

        public string Text { get; set; }
        public string Language { get; set; }

        public LanguageDocument(string text, string language)
        {
            Text = text;
            Language = language;
        }
    }
}
