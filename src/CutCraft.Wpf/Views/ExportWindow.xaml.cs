using System.IO;
using System.Windows;
using System.Windows.Controls;
using CutCraft.Core.Models;
using CutCraft.Media.Export;
using Microsoft.Win32;

namespace CutCraft.Wpf.Views;

public partial class ExportWindow : Window
{
    private readonly Project _project;
    private readonly IMediaExporter _exporter = new FFmpegProcessExporter();

    public ExportWindow(Project project)
    {
        InitializeComponent();
        _project = project;

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            $"{project.Name.Replace(" ", "_")}_export.mp4");

        OutputPathTextBox.Text = defaultPath;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "MP4 Video (*.mp4)|*.mp4|All files (*.*)|*.*",
            FileName = Path.GetFileName(OutputPathTextBox.Text),
            DefaultExt = ".mp4"
        };

        if (sfd.ShowDialog() == true)
        {
            OutputPathTextBox.Text = sfd.FileName;
        }
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputPathTextBox == null || FormatInfoTextBlock == null) return;

        var selectedTag = (PresetComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1080p";
        var currentPath = OutputPathTextBox.Text;
        var dir = Path.GetDirectoryName(currentPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(currentPath);

        if (selectedTag == "gif")
        {
            OutputPathTextBox.Text = Path.Combine(dir, $"{baseName}.gif");
            FormatInfoTextBlock.Text = "Format: High-Quality Animated GIF (No Audio)";
        }
        else if (selectedTag == "mp3")
        {
            OutputPathTextBox.Text = Path.Combine(dir, $"{baseName}.mp3");
            FormatInfoTextBlock.Text = "Format: MP3 Audio Only (192 kbps)";
        }
        else
        {
            OutputPathTextBox.Text = Path.Combine(dir, $"{baseName}.mp4");
            FormatInfoTextBlock.Text = "Format: MP4 (H.264 Video + AAC Stereo Audio)";
        }
    }

    private async void StartExportButton_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = OutputPathTextBox.Text;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show("Please choose a valid output destination file.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var settings = new ExportSettings
        {
            OutputPath = outputPath,
            Quality = ExportQuality.High,
            FrameRate = _project.FrameRate,
            NormalizeAudio = NormalizeAudioCheckBox.IsChecked == true,
            EnableAudioDucking = AudioDuckingCheckBox.IsChecked == true
        };

        var selectedTag = (PresetComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1080p";
        switch (selectedTag)
        {
            case "4k":
                settings.Resolution = ExportResolution.Uhd4k;
                settings.VideoBitrateKbps = 24000;
                break;
            case "720p":
                settings.Resolution = ExportResolution.Hd720p;
                settings.VideoBitrateKbps = 4500;
                break;
            case "vertical":
                settings.Resolution = ExportResolution.VerticalShorts;
                settings.VideoBitrateKbps = 8000;
                break;
            case "square":
                settings.Resolution = ExportResolution.Square;
                settings.VideoBitrateKbps = 7000;
                break;
            case "gif":
                settings.Format = ExportFormat.Gif;
                settings.Resolution = ExportResolution.Hd720p;
                break;
            case "mp3":
                settings.Format = ExportFormat.AudioOnlyMp3;
                settings.AudioBitrateKbps = 192;
                break;
            default:
                settings.Resolution = ExportResolution.Fhd1080p;
                settings.VideoBitrateKbps = 9000;
                break;
        }

        StartExportButton.IsEnabled = false;
        StatusTextBlock.Text = "Rendering & encoding video with FFmpeg...";

        var progress = new Progress<double>(p =>
        {
            ExportProgressBar.Value = p * 100;
            StatusTextBlock.Text = $"Exporting: {Math.Round(p * 100)}%";
        });

        var result = await _exporter.ExportProjectAsync(_project, settings, progress);

        StartExportButton.IsEnabled = true;
        if (result.Success)
        {
            StatusTextBlock.Text = "Export completed successfully!";
            MessageBox.Show($"Video export succeeded!\n\nFile saved to:\n{result.OutputFilePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        else
        {
            StatusTextBlock.Text = "Export encountered an error.";
            MessageBox.Show($"Export failed:\n{result.ErrorMessage}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
