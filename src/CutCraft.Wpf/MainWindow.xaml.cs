using System.IO;
using System.Windows;
using System.Windows.Input;
using CutCraft.Core.Models;
using CutCraft.Wpf.ViewModels;
using CutCraft.Wpf.Views;
using Microsoft.Win32;

namespace CutCraft.Wpf;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "CutCraft Project (*.cutcraft;*.json)|*.cutcraft;*.json|All files (*.*)|*.*",
            Title = "Open Video Project"
        };

        if (ofd.ShowDialog() == true)
        {
            Vm.OpenProject(ofd.FileName);
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "CutCraft Project (*.cutcraft)|*.cutcraft|JSON Project (*.json)|*.json",
            Title = "Save Video Project",
            FileName = $"{Vm.CurrentProject.Name}.cutcraft",
            DefaultExt = ".cutcraft"
        };

        if (sfd.ShowDialog() == true)
        {
            Vm.SaveProject(sfd.FileName);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var exportWindow = new ExportWindow(Vm.CurrentProject)
        {
            Owner = this
        };
        exportWindow.ShowDialog();
    }

    private async void ImportMedia_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Media Files (*.mp4;*.mkv;*.mov;*.avi;*.mp3;*.wav;*.png;*.jpg)|*.mp4;*.mkv;*.mov;*.avi;*.mp3;*.wav;*.png;*.jpg|All files (*.*)|*.*",
            Title = "Import Media Assets",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            foreach (var file in ofd.FileNames)
            {
                await Vm.ImportMediaAsync(file);
            }
        }
    }

    private void ImportSrt_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "SubRip Subtitles (*.srt)|*.srt|All files (*.*)|*.*",
            Title = "Import Subtitles"
        };

        if (ofd.ShowDialog() == true)
        {
            Vm.ImportSrtFile(ofd.FileName);
        }
    }

    private void ExportSrt_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "SubRip Subtitles (*.srt)|*.srt|All files (*.*)|*.*",
            Title = "Export Subtitles",
            DefaultExt = ".srt",
            FileName = $"{Vm.CurrentProject.Name}_subtitles.srt"
        };

        if (sfd.ShowDialog() == true)
        {
            Vm.ExportSrtFile(sfd.FileName);
        }
    }

    private async void AddMusicAtPlayhead_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Audio & Music Files (*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.ogg)|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.ogg|All files (*.*)|*.*",
            Title = $"Add Music at Current Point ({Vm.CurrentTimecode})",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            foreach (var file in ofd.FileNames)
            {
                await Vm.AddMusicAtPlayheadAsync(file);
            }
        }
    }

    private void Clip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Clip clip)
        {
            Vm.SelectedClip = clip;
            var track = Vm.CurrentProject.Tracks.FirstOrDefault(t => t.Clips.Contains(clip));
            if (track != null)
            {
                Vm.SelectedTrack = track;
            }
            Vm.Notifier.Announce($"Selected clip: {clip.Name}");
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.M && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            AddMusicAtPlayhead_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
