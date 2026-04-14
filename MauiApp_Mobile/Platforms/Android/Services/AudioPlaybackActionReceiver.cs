#if ANDROID
using Android.App;
using Android.Content;

namespace MauiApp_Mobile.Services;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([
    AndroidAudioPlaybackNotificationManager.ActionToggle,
    AndroidAudioPlaybackNotificationManager.ActionStop,
    AndroidAudioPlaybackNotificationManager.ActionPrevious,
    AndroidAudioPlaybackNotificationManager.ActionNext,
    AndroidAudioPlaybackNotificationManager.ActionSeekBackward,
    AndroidAudioPlaybackNotificationManager.ActionSeekForward
])]
public sealed class AudioPlaybackActionReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var action = intent?.Action;
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            switch (action)
            {
                case AndroidAudioPlaybackNotificationManager.ActionToggle:
                    await PlaybackCoordinatorService.Instance.TogglePauseResumeAsync();
                    break;
                case AndroidAudioPlaybackNotificationManager.ActionStop:
                    await PlaybackCoordinatorService.Instance.StopAsync();
                    break;
                case AndroidAudioPlaybackNotificationManager.ActionPrevious:
                    await PlaybackCoordinatorService.Instance.PlayPreviousAsync();
                    break;
                case AndroidAudioPlaybackNotificationManager.ActionNext:
                    await PlaybackCoordinatorService.Instance.PlayNextAsync();
                    break;
                case AndroidAudioPlaybackNotificationManager.ActionSeekBackward:
                    await PlaybackCoordinatorService.Instance.SeekByAsync(TimeSpan.FromSeconds(-5));
                    break;
                case AndroidAudioPlaybackNotificationManager.ActionSeekForward:
                    await PlaybackCoordinatorService.Instance.SeekByAsync(TimeSpan.FromSeconds(5));
                    break;
            }
        });
    }
}
#endif
