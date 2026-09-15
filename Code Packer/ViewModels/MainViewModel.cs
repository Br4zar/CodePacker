using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CodePacker.Models;
using CodePacker.Services;
using Microsoft.Win32;

namespace CodePacker.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly FileScannerService _scanner = new();
    private readonly CodeProcessorService _processor = new();
    private readonly TokenEstimatorService _tokenService = new();
    private readonly SettingsService _settingsService = new();
    private readonly PdfExportService _pdfService = new();

    private AppSettings _settings;
    private List<FileItem> _allFiles = new();
    private string _currentFolderPath = string.Empty;
    private string _projectName = "Project";
    private string _statusText = "Готов к работе";
    private bool _isLoading;
    private string _searchQuery = string.Empty;

    public MainViewModel()
    {
        // Загружаем настройки напрямую.
        // Не используем здесь свойства, setters которых вызывают Save().
        _settings = _settingsService.LoadSettings();

        // Защита от null в сохранённом JSON.
        _settings.CustomExtensions ??= string.Empty;
        _settings.CustomIgnorePatterns ??= string.Empty;
        _settings.QuickSlots ??= new List<QuickSlot>();
        _settings.RecentFolders ??= new List<string>();

        _settings.PdfFontSize = Math.Clamp(_settings.PdfFontSize, 4, 14);

        Presets = new ObservableCollection<string>(
            TokenEstimatorService.Presets.Keys);

        // Меняем поле настроек, а не свойство SelectedPreset.
        // Поэтому Save() сейчас не вызывается.
        if (string.IsNullOrWhiteSpace(_settings.SelectedPreset) ||
            !Presets.Contains(_settings.SelectedPreset))
        {
            _settings.SelectedPreset = Presets[0];
        }

        // Сначала создаём все шесть слотов.
        // Сохранённые проекты возвращаем на их исходные позиции.
        QuickSlots = new ObservableCollection<QuickSlot>();

        for (int index = 0; index < 6; index++)
        {
            var savedSlot = _settings.QuickSlots.FirstOrDefault(
                slot => slot is not null && slot.SlotIndex == index);

            QuickSlots.Add(savedSlot ?? new QuickSlot
            {
                SlotIndex = index,
                Name = $"Слот {index + 1}",
                FolderPath = string.Empty,
                FileCount = 0
            });
        }

        // Затем создаём команды.
        SelectFolderCommand = new RelayCommand(
            async () => await PickFolderAsync());

        CopyCommand = new RelayCommand(CopyToClipboard);
        ExportTxtCommand = new RelayCommand(ExportTxt);
        ExportPdfCommand = new RelayCommand(ExportPdf);

        SelectAllCommand = new RelayCommand(
            () => SetAllSelection(true));

        DeselectAllCommand = new RelayCommand(
            () => SetAllSelection(false));

        PinToSlotCommand = new RelayCommand(PinCurrentToSlot);

        LoadSlotCommand = new RelayCommand(
            async parameter => await LoadSlotAsync(parameter));

        // Всё необходимое уже создано.
        Recalculate();
    }
    public ObservableCollection<TreeNodeItem> RootNodes { get; } = new();
    public ObservableCollection<OptimizationHint> Hints { get; } = new();
    public ObservableCollection<QuickSlot> QuickSlots { get; }
    public ObservableCollection<string> Presets { get; }

    public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public string SearchQuery { get => _searchQuery; set { SetProperty(ref _searchQuery, value); FilterTree(); } }

    public string CustomExtensions { get => _settings.CustomExtensions; set { _settings.CustomExtensions = value; OnPropertyChanged(); Save(); } }
    public string CustomIgnorePatterns { get => _settings.CustomIgnorePatterns; set { _settings.CustomIgnorePatterns = value; OnPropertyChanged(); Save(); } }
    public bool RemoveWhitespace { get => _settings.RemoveWhitespace; set { _settings.RemoveWhitespace = value; OnPropertyChanged(); Recalculate(); Save(); } }
    public bool RemoveComments { get => _settings.RemoveComments; set { _settings.RemoveComments = value; OnPropertyChanged(); Recalculate(); Save(); } }
    public bool MinimalHeader { get => _settings.MinimalHeader; set { _settings.MinimalHeader = value; OnPropertyChanged(); Recalculate(); Save(); } }
    public bool SmartJsonMinify { get => _settings.SmartJsonMinify; set { _settings.SmartJsonMinify = value; OnPropertyChanged(); Recalculate(); Save(); } }
    public bool IncludeImagesInPdf { get => _settings.IncludeImagesInPdf; set { _settings.IncludeImagesInPdf = value; OnPropertyChanged(); Recalculate(); Save(); } }
    public int PdfFontSize { get => _settings.PdfFontSize; set { _settings.PdfFontSize = value; OnPropertyChanged(); Save(); } }

    public string SelectedPreset
    {
        get => _settings.SelectedPreset;
        set { _settings.SelectedPreset = value; OnPropertyChanged(); Recalculate(); Save(); }
    }

    // Статистика
    private int _codeFilesCount;
    public int CodeFilesCount { get => _codeFilesCount; set => SetProperty(ref _codeFilesCount, value); }

    private int _imageFilesCount;
    public int ImageFilesCount { get => _imageFilesCount; set => SetProperty(ref _imageFilesCount, value); }

    private string _rawSizeFormatted = "0 B";
    public string RawSizeFormatted { get => _rawSizeFormatted; set => SetProperty(ref _rawSizeFormatted, value); }

    private string _packedSizeFormatted = "0 B";
    public string PackedSizeFormatted { get => _packedSizeFormatted; set => SetProperty(ref _packedSizeFormatted, value); }

    private int _savingsPercent;
    public int SavingsPercent { get => _savingsPercent; set => SetProperty(ref _savingsPercent, value); }

    private int _estimatedTokens;
    public int EstimatedTokens { get => _estimatedTokens; set => SetProperty(ref _estimatedTokens, value); }

    private int _tokenPercent;
    public int TokenPercent { get => _tokenPercent; set => SetProperty(ref _tokenPercent, value); }

    public RelayCommand SelectFolderCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand ExportTxtCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand PinToSlotCommand { get; }
    public RelayCommand LoadSlotCommand { get; }

    public async Task OpenDirectoryPathAsync(string path)
    {
        if (!Directory.Exists(path)) return;
        _currentFolderPath = path;
        ProjectName = Path.GetFileName(path);
        IsLoading = true;
        StatusText = "Сканирование файлов...";

        try
        {
            var progress = new Progress<string>(s => StatusText = s);
            _allFiles = await _scanner.ScanDirectoryAsync(path, CustomExtensions, CustomIgnorePatterns, progress);
            BuildTree();
            Recalculate();
            StatusText = $"Загружено {_allFiles.Count} файлов";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PickFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку проекта" };
        if (dialog.ShowDialog() == true)
        {
            await OpenDirectoryPathAsync(dialog.FolderName);
        }
    }

    private void BuildTree()
    {
        RootNodes.Clear();
        var nodeDict = new Dictionary<string, TreeNodeItem>();

        foreach (var file in _allFiles)
        {
            var parts = file.RelativePath.Split('/');
            string currentPath = "";
            TreeNodeItem? parentNode = null;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                bool isFile = i == parts.Length - 1;

                if (!nodeDict.TryGetValue(currentPath, out var node))
                {
                    node = new TreeNodeItem
                    {
                        Name = part,
                        FullPath = currentPath,
                        IsFolder = !isFile,
                        FileId = isFile ? file.Id : null,
                        SizeBytes = isFile ? file.SizeBytes : 0,
                        Parent = parentNode
                    };

                    node.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TreeNodeItem.IsChecked) && isFile)
                        {
                            file.IsSelected = node.IsChecked == true;
                            Recalculate();
                        }
                    };

                    nodeDict[currentPath] = node;

                    if (parentNode == null)
                        RootNodes.Add(node);
                    else
                        parentNode.Children.Add(node);
                }

                parentNode = node;
            }
        }
    }

    private void FilterTree()
    {
        // Поиск по относительному пути
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SetVisibility(RootNodes, true);
            return;
        }

        bool MatchNode(TreeNodeItem node)
        {
            if (!node.IsFolder)
                return node.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

            bool anyChildMatched = false;
            foreach (var child in node.Children)
            {
                if (MatchNode(child)) anyChildMatched = true;
            }
            return anyChildMatched;
        }

        foreach (var root in RootNodes) MatchNode(root);
    }

    private void SetVisibility(IEnumerable<TreeNodeItem> nodes, bool isVisible)
    {
        foreach (var n in nodes) SetVisibility(n.Children, isVisible);
    }

    private void SetAllSelection(bool isSelected)
    {
        foreach (var root in RootNodes) root.IsChecked = isSelected;
        foreach (var f in _allFiles) f.IsSelected = isSelected;
        Recalculate();
    }

    public string GeneratePackedText()
    {
        var selected = _allFiles.Where(f => f.IsSelected).ToList();
        var sb = new StringBuilder();

        var codeFiles = selected.Where(f => !f.IsImage).ToList();
        var imgFiles = selected.Where(f => f.IsImage).ToList();
        long totalSize = selected.Sum(f => f.SizeBytes);

        if (MinimalHeader)
        {
            var imgNote = imgFiles.Count > 0 ? $" + {imgFiles.Count} img" : "";
            sb.AppendLine($"# {ProjectName} | {codeFiles.Count} files{imgNote} | {TokenEstimatorService.FormatSize(totalSize)}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"=== {ProjectName} ===");
            sb.AppendLine($"Files: {codeFiles.Count} | Images: {imgFiles.Count} | Size: {TokenEstimatorService.FormatSize(totalSize)}");
            sb.AppendLine();
            for (int i = 0; i < selected.Count; i++)
                sb.AppendLine($"{i + 1}. {selected[i].RelativePath} ({TokenEstimatorService.FormatSize(selected[i].SizeBytes)})");
            sb.AppendLine("---");
            sb.AppendLine();
        }

        foreach (var file in selected)
        {
            sb.AppendLine($">>> {file.RelativePath}");
            if (file.IsImage)
            {
                sb.AppendLine($"[IMG:{file.RelativePath}]");
            }
            else
            {
                var processed = _processor.ProcessCode(file.RawContent, file.RelativePath, RemoveWhitespace, RemoveComments, SmartJsonMinify);
                sb.AppendLine(processed);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void Recalculate()
    {
        var selected = _allFiles.Where(f => f.IsSelected).ToList();
        CodeFilesCount = selected.Count(f => !f.IsImage);
        ImageFilesCount = selected.Count(f => f.IsImage);

        long raw = selected.Sum(f => f.SizeBytes);
        RawSizeFormatted = TokenEstimatorService.FormatSize(raw);

        var packed = GeneratePackedText();
        long packedBytes = Encoding.UTF8.GetByteCount(packed);
        PackedSizeFormatted = TokenEstimatorService.FormatSize(packedBytes);

        SavingsPercent = raw > 0
            ? -(int)Math.Max(0, (1.0 - (double)packedBytes / raw) * 100)
            : 0;

        int textTokens = _tokenService.EstimateTextTokens(packed);
        int imgTokens = selected.Where(f => f.IsImage).Sum(i => _tokenService.EstimateImageTokens(i.ImageWidth, i.ImageHeight));
        EstimatedTokens = textTokens + (IncludeImagesInPdf ? imgTokens : 0);

        int budget = TokenEstimatorService.Presets.TryGetValue(SelectedPreset, out int b) ? b : 150_000;
        TokenPercent = (int)Math.Min(100, (EstimatedTokens * 100.0) / budget);

        Hints.Clear();
        foreach (var hint in _tokenService.GenerateHints(_allFiles))
            Hints.Add(hint);
    }

    private void CopyToClipboard()
    {
        var text = GeneratePackedText();
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            StatusText = "✓ Скопировано в буфер обмена!";
        }
    }

    private void ExportTxt()
    {
        var dialog = new SaveFileDialog { FileName = $"{ProjectName}.txt", Filter = "Text files (*.txt)|*.txt" };
        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, GeneratePackedText());
            StatusText = $"✓ Сохранено: {Path.GetFileName(dialog.FileName)}";
        }
    }

    private void ExportPdf()
    {
        var dialog = new SaveFileDialog { FileName = $"{ProjectName}.pdf", Filter = "PDF document (*.pdf)|*.pdf" };
        if (dialog.ShowDialog() == true)
        {
            var content = GeneratePackedText();
            var images = _allFiles.Where(f => f.IsSelected && f.IsImage).ToList();
            _pdfService.ExportToPdf(dialog.FileName, ProjectName, content, images, PdfFontSize);
            StatusText = $"✓ PDF сохранён: {Path.GetFileName(dialog.FileName)}";
        }
    }

    private void PinCurrentToSlot(object? slotObj)
    {
        if (slotObj is int index && index >= 0 && index < QuickSlots.Count && !string.IsNullOrEmpty(_currentFolderPath))
        {
            QuickSlots[index] = new QuickSlot
            {
                SlotIndex = index,
                Name = ProjectName,
                FolderPath = _currentFolderPath,
                FileCount = _allFiles.Count,
                LastUsed = DateTime.Now
            };
            Save();
            StatusText = $"Папка закреплена в Слот {index + 1}";
        }
    }

    private async Task LoadSlotAsync(object? slotObj)
    {
        if (slotObj is QuickSlot slot && !string.IsNullOrEmpty(slot.FolderPath))
        {
            await OpenDirectoryPathAsync(slot.FolderPath);
        }
    }

    private void Save()
    {
        _settings.QuickSlots = QuickSlots.Where(s => !string.IsNullOrEmpty(s.FolderPath)).ToList();
        _settingsService.SaveSettings(_settings);
    }
}