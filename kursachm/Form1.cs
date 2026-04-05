using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace kursachm
{
    public partial class Form1 : Form
    {
        private MarkovChainBackoffClassifier classifier;   // изменён тип

        public Form1()
        {
            InitializeComponent();
            // Параметры: порядок цепи = 4, вес цитат = 0.05, fallbackProb = 1e-9
            classifier = new MarkovChainBackoffClassifier(maxOrder: 4, quoteWeight: 0.05);
        }

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

        private void BtnTrain_Click(object sender, EventArgs e)
        {
            string trainPath = txtTrainPath.Text.Trim();
            if (string.IsNullOrEmpty(trainPath) || !Directory.Exists(trainPath))
            {
                MessageBox.Show("Укажите существующую папку обучения.");
                return;
            }

            List<LanguageDocument> trainDocs = new List<LanguageDocument>();

            // Пытаемся загрузить WiLI
            if (TryFindWiliFiles(trainPath, out string textsFile, out string labelsFile))
            {
                Log($"Обнаружены файлы WiLI:\n  Тексты: {Path.GetFileName(textsFile)}\n  Метки: {Path.GetFileName(labelsFile)}");
                trainDocs = LoadWiliData(textsFile, labelsFile);
            }
            else
            {
                // Если WiLI не найдены, загружаем из подпапок
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

        private bool TryFindWiliFiles(string folder, out string textsFile, out string labelsFile)
        {
            textsFile = null;
            labelsFile = null;

            var allTxtFiles = Directory.GetFiles(folder, "*.txt");
            var trainFiles = allTxtFiles
                .Where(f => Path.GetFileName(f).IndexOf("train", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (trainFiles.Count < 2) return false;

            var fileInfos = trainFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.Length)
                .ToList();

            textsFile = fileInfos.First().FullName;
            labelsFile = fileInfos.Last().FullName;
            return true;
        }

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

    // Простая модель документа
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