using System.Runtime.Versioning;

namespace CutCraft.Core.Accessibility;

public interface IAudioCueService
{
    bool IsEnabled { get; set; }
    void PlayCutCue();
    void PlayBoundaryCue();
    void PlayUndoCue();
    void PlayRedoCue();
    void PlaySeekCue();
}

[SupportedOSPlatform("windows")]
public class SystemAudioCueService : IAudioCueService
{
    public bool IsEnabled { get; set; } = true;

    public void PlayCutCue()
    {
        if (!IsEnabled) return;
        PlayBeep(1200, 40);
    }

    public void PlayBoundaryCue()
    {
        if (!IsEnabled) return;
        PlayBeep(400, 80);
    }

    public void PlayUndoCue()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                Console.Beep(800, 40);
                Console.Beep(500, 40);
            }
            catch { }
        });
    }

    public void PlayRedoCue()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                Console.Beep(500, 40);
                Console.Beep(800, 40);
            }
            catch { }
        });
    }

    public void PlaySeekCue()
    {
        if (!IsEnabled) return;
        PlayBeep(1800, 15);
    }

    private void PlayBeep(int frequency, int durationMs)
    {
        Task.Run(() =>
        {
            try
            {
                Console.Beep(frequency, durationMs);
            }
            catch
            {
                // In non-interactive or non-beep environments, gracefully ignore
            }
        });
    }
}
