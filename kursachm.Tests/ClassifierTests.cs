using System.Collections.Generic;
using Xunit;

namespace kursachm.Tests
{
    /// <summary>
    /// Набор модульных тестов для классификатора языка на основе марковских цепей.
    /// Тесты используют небольшую встроенную обучающую выборку (6 языков, по 2 документа).
    /// </summary>
    public class MarkovChainClassifierTests
    {
        /// <summary>
        /// Тест: английский текст должен быть определён как "eng" с высокой уверенностью.
        /// </summary>
        [Fact]
        public void Classify_EnglishText_ReturnsEng()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string englishText = "The United Kingdom is a country. English is spoken here.";
            var (lang, confidence, _) = classifier.Classify(englishText);

            Assert.Equal("eng", lang);
            Assert.True(confidence > 0.9, "Уверенность для английского текста должна быть выше 90%");
        }

        /// <summary>
        /// Тест: русский текст должен быть определён как "rus" с высокой уверенностью.
        /// </summary>
        [Fact]
        public void Classify_RussianText_ReturnsRus()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string russianText = "Россия — страна. Русский язык используется.";
            var (lang, confidence, _) = classifier.Classify(russianText);

            Assert.Equal("rus", lang);
            Assert.True(confidence > 0.9, "Уверенность для русского текста должна быть выше 90%");
        }

        /// <summary>
        /// Тест: французский текст должен быть определён как "fra" с высокой уверенностью.
        /// </summary>
        [Fact]
        public void Classify_FrenchText_ReturnsFra()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string frenchText = "La France est un pays. Le français est parlé.";
            var (lang, confidence, _) = classifier.Classify(frenchText);

            Assert.Equal("fra", lang);
            Assert.True(confidence > 0.9, "Уверенность для французского текста должна быть выше 90%");
        }

        /// <summary>
        /// Тест: немецкий текст должен быть определён как "deu" с высокой уверенностью.
        /// </summary>
        [Fact]
        public void Classify_GermanText_ReturnsDeu()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string germanText = "Deutschland ist ein Land. Die deutsche Sprache ist schön.";
            var (lang, confidence, _) = classifier.Classify(germanText);

            Assert.Equal("deu", lang);
            Assert.True(confidence > 0.9, "Уверенность для немецкого текста должна быть выше 90%");
        }

        /// <summary>
        /// Тест: итальянский текст должен быть определён как "ita" с высокой уверенностью.
        /// </summary>
        [Fact]
        public void Classify_ItalianText_ReturnsIta()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string italianText = "L'Italia è una repubblica. La lingua italiana è bellissima.";
            var (lang, confidence, _) = classifier.Classify(italianText);

            Assert.Equal("ita", lang);
            Assert.True(confidence > 0.9, "Уверенность для итальянского текста должна быть выше 90%");
        }

        /// <summary>
        /// Тест: испанский текст должен быть определён как "spa" с высокой уверенностью.
        /// </summary>
        [Fact]
        public void Classify_SpanishText_ReturnsSpa()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string spanishText = "España es un país. El español se habla en muchos países.";
            var (lang, confidence, _) = classifier.Classify(spanishText);

            Assert.Equal("spa", lang);
            Assert.True(confidence > 0.9, "Уверенность для испанского текста должна быть выше 90%");
        }

        /// <summary>
        /// Тест: текст с длинной цитатой на английском внутри русского текста.
        /// Благодаря пониженному весу цитат, основным языком должен остаться русский.
        /// </summary>
        [Fact]
        public void Classify_QuoteTest_ReturnsRus()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string mixedText = "Привет! Это короткое введение.\n" +
                               "\"English is a very long language that contains many words and sentences. " +
                               "The quick brown fox jumps over the lazy dog. This is an example of a long quote " +
                               "in English that takes up most of the text.\"\n" +
                               "Конец.";
            var (lang, confidence, _) = classifier.Classify(mixedText);

            Assert.Equal("rus", lang);
            Assert.True(confidence > 0.9, "Русский язык должен быть определён, несмотря на длинную английскую цитату");
        }

        /// <summary>
        /// Тест: смешанный текст (русский + английский) без кавычек.
        /// Преобладает русский – ожидаем русский язык (уверенность может быть не максимальной).
        /// </summary>
        [Fact]
        public void Classify_MixedRussianEnglish_ReturnsRus()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string mixed = "Привет! How are you? Сегодня хорошая погода. Let's go for a walk. Я люблю программирование.";
            var (lang, confidence, _) = classifier.Classify(mixed);

            // В данном смешанном тексте русских предложений больше, поэтому ожидаем "rus"
            Assert.Equal("rus", lang);
            // Уверенность может быть не очень высокой, но выше 50%
            Assert.True(confidence > 0.5, "Уверенность для смешанного текста должна быть выше 50%");
        }

        /// <summary>
        /// Тест: очень короткий текст (одно слово). Классификатор должен вернуть какой-то язык,
        /// но уверенность может быть низкой.
        /// </summary>
        [Fact]
        public void Classify_ShortWord_ReturnsSomeLanguage()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string word = "hello";
            var (lang, confidence, _) = classifier.Classify(word);

            Assert.NotNull(lang);
            // Уверенность может быть невысокой, но язык должен определиться (скорее всего английский)
            Assert.True(confidence > 0, "Уверенность должна быть положительной");
        }

        /// <summary>
        /// Тест: текст на неизвестном языке (условная абракадабра).
        /// Классификатор всё равно вернёт ближайший язык, но уверенность будет низкой.
        /// </summary>
        [Fact]
        public void Classify_Gibberish_ReturnsSomeLanguageButLowConfidence()
        {
            var classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05);
            var trainDocs = GetTrainingSet();
            classifier.Train(trainDocs);

            string gibberish = "qwertyuiop asdfghjkl zxcvbnm 12345";
            var (lang, confidence, _) = classifier.Classify(gibberish);

            Assert.NotNull(lang);
            // Уверенность должна быть невысокой, так как текст не похож ни на один язык
            Assert.True(confidence < 0.8, "Для бессмысленного текста уверенность не должна быть высокой");
        }

        /// <summary>
        /// Возвращает небольшой сбалансированный обучающий набор из 6 языков (по 2 документа на язык).
        /// </summary>
        private List<LanguageDocument> GetTrainingSet()
        {
            return new List<LanguageDocument>
            {
                // Английский
                new LanguageDocument("England is a country. English is spoken.", "eng"),
                new LanguageDocument("The quick brown fox jumps over the lazy dog.", "eng"),
                // Французский
                new LanguageDocument("La France est un pays. Le français est parlé.", "fra"),
                new LanguageDocument("Bonjour tout le monde. Comment allez-vous?", "fra"),
                // Немецкий
                new LanguageDocument("Deutschland ist ein Land. Deutsch wird gesprochen.", "deu"),
                new LanguageDocument("Guten Morgen. Wie geht es dir?", "deu"),
                // Итальянский
                new LanguageDocument("L'Italia è un paese. L'italiano è parlato.", "ita"),
                new LanguageDocument("Buongiorno. Come stai?", "ita"),
                // Испанский
                new LanguageDocument("España es un país. El español es hablado.", "spa"),
                new LanguageDocument("Buenos días. ¿Cómo estás?", "spa"),
                // Русский
                new LanguageDocument("Россия — страна. Русский язык используется.", "rus"),
                new LanguageDocument("Привет, как дела? Хорошая погода.", "rus"),
            };
        }
    }
}