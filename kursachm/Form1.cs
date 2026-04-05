using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace kursachm
{
    /// <summary>
    /// Главная форма приложения для определения языка текста с использованием марковских цепей.
    /// Позволяет выбрать обучающую папку (WiLI или подпапки с языками), обучить модель,
    /// выбрать тестовый файл и классифицировать его язык.
    /// </summary>
    public partial class Form1 : Form
    {
        private MarkovChainBackoffClassifier classifier;

        /// <summary>
        /// Конструктор формы. Инициализирует компоненты и создаёт классификатор
        /// с параметрами: порядок цепи = 6, вес цитат = 0.05, fallbackProb = 1e-9.
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            classifier = new MarkovChainBackoffClassifier(maxOrder: 6, quoteWeight: 0.05, fallbackProb: 1e-9);
        }

        /// <summary>
        /// Обработчик нажатия кнопки выбора папки с обучающими данными.
        /// Открывает диалог выбора папки и записывает путь в текстовое поле txtTrainPath.
        /// </summary>
        private void BtnSelectTrainFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с данными (WiLI или подпапки языков)";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtTrainPath.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки выбора тестового файла.
        /// Открывает диалог выбора файла (только .txt) и записывает путь в txtTestFile.
        /// </summary>
        private void BtnSelectTestFile_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtTestFile.Text = dialog.FileName;
                }
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки обучения. Загружает документы из указанной папки
        /// (поддерживается формат WiLI с файлами x_train.txt и y_train.txt, либо структура с подпапками языков).
        /// Вызывает метод Train классификатора и выводит статистику в лог.
        /// </summary>
        private void BtnTrain_Click(object sender, EventArgs e)
        {
            string trainPath = txtTrainPath.Text.Trim();
            if (string.IsNullOrEmpty(trainPath) || !Directory.Exists(trainPath))
            {
                MessageBox.Show("Укажите существующую папку обучения.");
                return;
            }

            List<LanguageDocument> trainDocs = new List<LanguageDocument>();

            // Попытка загрузить данные в формате WiLI (файлы с "train" в имени)
            if (TryFindWiliFiles(trainPath, out string textsFile, out string labelsFile))
            {
                Log($"Обнаружены файлы WiLI:\n  Тексты: {Path.GetFileName(textsFile)}\n  Метки: {Path.GetFileName(labelsFile)}");
                trainDocs = LoadWiliData(textsFile, labelsFile);
            }
            else
            {
                // Иначе загружаем из подпапок, где имя подпапки = метка языка
                Log("Файлы WiLI не найдены. Загружаем данные из подпапок...");
                trainDocs = LoadFromSubfolders(trainPath);
            }

            if (trainDocs.Count == 0)
            {
                MessageBox.Show("Не найдено ни одного документа для обучения.");
                return;
            }

            var languages = trainDocs.Select(d => d.Language).Distinct().ToList();
            Log($"Загружено документов: {trainDocs.Count}. Языков: {languages.Count} (первые 10: {string.Join(", ", languages.Take(10))})");
            Log("Начинается обучение...");
            classifier.Train(trainDocs);
            Log("Обучение завершено.");
            Log($"Всего обучено языков: {classifier.GetLanguages().Count()}");
        }

        /// <summary>
        /// Обработчик нажатия кнопки классификации. Читает содержимое выбранного тестового файла,
        /// вызывает метод Classify классификатора и выводит результат (основной язык, уверенность, топ-5 кандидатов) в лог.
        /// </summary>
        private void BtnClassify_Click(object sender, EventArgs e)
        {
            string testFile = txtTestFile.Text.Trim();
            if (string.IsNullOrEmpty(testFile) || !File.Exists(testFile))
            {
                MessageBox.Show("Укажите существующий тестовый файл.");
                return;
            }

            try
            {
                string content = File.ReadAllText(testFile);
                var (bestLanguage, confidence, topCandidates) = classifier.Classify(content);

                if (bestLanguage == null)
                {
                    Log("Классификатор не обучен.");
                    return;
                }

                Log($"{Path.GetFileName(testFile)}: язык = {bestLanguage} (уверенность: {confidence:P2})");
                Log("Топ-5 кандидатов:");
                foreach (var (lang, logScore, prob) in topCandidates)
                {
                    Log($"  {lang}: лог-оценка = {logScore:F2}, вероятность = {prob:P2}");
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при классификации: {ex.Message}");
            }
        }

        /// <summary>
        /// Пытается найти файлы WiLI в указанной папке. Ищет два текстовых файла,
        /// содержащих в имени подстроку "train" (регистронезависимо). Возвращает пути к файлам
        /// с текстами и метками. Предполагается, что файл с текстами имеет больший размер.
        /// </summary>
        /// <param name="folder">Путь к папке с данными.</param>
        /// <param name="textsFile">Возвращаемый путь к файлу с текстами.</param>
        /// <param name="labelsFile">Возвращаемый путь к файлу с метками языков.</param>
        /// <returns>True, если файлы найдены, иначе False.</returns>
        private bool TryFindWiliFiles(string folder, out string textsFile, out string labelsFile)
        {
            textsFile = null;
            labelsFile = null;

            var allTxtFiles = Directory.GetFiles(folder, "*.txt");
            var trainFiles = allTxtFiles
                .Where(f => Path.GetFileName(f).IndexOf("train", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (trainFiles.Count < 2) return false;

            // Сортируем по размеру: самый большой – тексты, самый маленький – метки
            var fileInfos = trainFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.Length)
                .ToList();

            textsFile = fileInfos.First().FullName;
            labelsFile = fileInfos.Last().FullName;
            return true;
        }

        /// <summary>
        /// Загружает данные из файлов WiLI (тексты и метки) в список LanguageDocument.
        /// Предполагается, что строки в обоих файлах синхронизированы.
        /// </summary>
        /// <param name="textsFile">Путь к файлу с текстами.</param>
        /// <param name="labelsFile">Путь к файлу с метками.</param>
        /// <returns>Список загруженных документов.</returns>
        private List<LanguageDocument> LoadWiliData(string textsFile, string labelsFile)
        {
            var docs = new List<LanguageDocument>();
            try
            {
                using (var textsReader = new StreamReader(textsFile))
                using (var labelsReader = new StreamReader(labelsFile))
                {
                    string textLine, labelLine;
                    while ((textLine = textsReader.ReadLine()) != null &&
                           (labelLine = labelsReader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(textLine)) continue;
                        docs.Add(new LanguageDocument(textLine.Trim(), labelLine.Trim()));
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка загрузки WiLI: {ex.Message}");
            }
            return docs;
        }

        /// <summary>
        /// Загружает документы из структуры папок, где каждая подпапка соответствует языку,
        /// а внутри лежат текстовые файлы на этом языке.
        /// </summary>
        /// <param name="trainPath">Путь к корневой папке с подпапками языков.</param>
        /// <returns>Список загруженных документов.</returns>
        private List<LanguageDocument> LoadFromSubfolders(string trainPath)
        {
            var docs = new List<LanguageDocument>();
            var languageDirs = Directory.GetDirectories(trainPath);
            if (languageDirs.Length == 0)
            {
                MessageBox.Show("В указанной папке нет подпапок с языками.");
                return docs;
            }

            foreach (var langDir in languageDirs)
            {
                string language = Path.GetFileName(langDir);
                foreach (var file in Directory.GetFiles(langDir, "*.txt"))
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        docs.Add(new LanguageDocument(content, language));
                    }
                    catch (Exception ex)
                    {
                        Log($"Ошибка чтения файла {file}: {ex.Message}");
                    }
                }
            }
            return docs;
        }

        /// <summary>
        /// Добавляет сообщение в лог-текстовое поле с временной меткой.
        /// Потокобезопасный метод (использует Invoke при необходимости).
        /// </summary>
        /// <param name="message">Текст сообщения.</param>
        private void Log(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(Log), message);
            }
            else
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            }
        }
    }

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