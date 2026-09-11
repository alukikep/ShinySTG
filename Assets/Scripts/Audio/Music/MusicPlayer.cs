using System.Collections;
using UnityEngine;

namespace ShinySTG.Audio
{
    /// <summary>
    /// BGM 播放器 —— 由 AudioSystem 持有,提供:
    ///   - PlayTrack(track, crossfade): 切到单首
    ///   - PlayPlaylist(playlist): 顺序/随机播放整个列表
    ///   - Pause() / Resume(): 暂停/恢复(配合 timeScale=0 也可工作)
    ///   - Stop(fadeOut): 停止
    ///
    /// 交叉淡化实现:
    ///   - 内部维护 2 个 MusicChannel(A/B)
    ///   - 切歌时:当前正在播放的 → 标为「淡出」,新 track 放到另一个 channel → 「淡入」
    ///   - 在 CrossfadeDuration 秒内,每帧从 BusMixer 读 Bgm 总线音量 × 各自进度 写入 _source.volume
    /// </summary>
    public class MusicPlayer
    {
        readonly AudioSystem _owner;
        MusicChannel _channelA;
        MusicChannel _channelB;
        MusicChannel _current;
        MusicChannel _fading;

        BgmPlaylist _activePlaylist;
        int[] _playlistOrder;
        int _playlistCursor;
        Coroutine _playlistCo;

        public MusicChannel CurrentChannel => _current;
        public BgmTrack CurrentTrack => _current?.CurrentTrack;

        public MusicPlayer(AudioSystem owner)
        {
            _owner = owner;
            _channelA = CreateChannel("MusicA");
            _channelB = CreateChannel("MusicB");
        }

        MusicChannel CreateChannel(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_owner.transform, false);
            return go.AddComponent<MusicChannel>();
        }

        public void PlayTrack(BgmTrack track, float crossfadeDuration = 1.5f)
        {
            StopPlaylistInternal();

            if (track == null || track.Clip == null)
            {
                Stop(crossfadeDuration);
                return;
            }

            float bgmBusVolume = _owner.BusMixer.GetCurrentVolume(AudioBusKind.Bgm);

            MusicChannel next = (_current == _channelA) ? _channelB : _channelA;
            next.Play(track, track.DefaultVolume * bgmBusVolume);

            StartCrossfade(_current, next, crossfadeDuration);
            _current = next;
        }

        public void PlayPlaylist(BgmPlaylist playlist)
        {
            if (playlist == null || playlist.Tracks == null || playlist.Tracks.Length == 0)
            {
                Stop(playlist != null ? playlist.CrossfadeDuration : 1.5f);
                return;
            }

            StopPlaylistInternal();
            _activePlaylist = playlist;

            int n = playlist.Tracks.Length;
            _playlistOrder = new int[n];
            for (int i = 0; i < n; i++) _playlistOrder[i] = i;
            if (playlist.Shuffle)
            {
                for (int i = n - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (_playlistOrder[i], _playlistOrder[j]) = (_playlistOrder[j], _playlistOrder[i]);
                }
            }
            _playlistCursor = 0;
            PlayPlaylistInternal(0, playlist.CrossfadeDuration);
        }

        void PlayPlaylistInternal(int indexInOrder, float crossfade)
        {
            if (_activePlaylist == null || _playlistOrder == null) return;
            if (indexInOrder >= _playlistOrder.Length)
            {
                if (_activePlaylist.Loop)
                {
                    if (_activePlaylist.Shuffle)
                    {
                        for (int i = _playlistOrder.Length - 1; i > 0; i--)
                        {
                            int j = Random.Range(0, i + 1);
                            (_playlistOrder[i], _playlistOrder[j]) = (_playlistOrder[j], _playlistOrder[i]);
                        }
                    }
                    _playlistCursor = 0;
                    indexInOrder = 0;
                }
                else return;
            }

            var track = _activePlaylist.Tracks[_playlistOrder[indexInOrder]];
            _playlistCursor = indexInOrder;
            PlayTrack(track, crossfade);
        }

        void StopPlaylistInternal()
        {
            if (_playlistCo != null)
            {
                _owner.StopCoroutine(_playlistCo);
                _playlistCo = null;
            }
            _activePlaylist = null;
            _playlistOrder = null;
            _playlistCursor = 0;
        }

        public void Pause()
        {
            if (_current != null) _current.Source.Pause();
            if (_fading != null) _fading.Source.Pause();
        }

        public void Resume()
        {
            if (_current != null) _current.Source.UnPause();
            if (_fading != null) _fading.Source.UnPause();
        }

        public void Stop(float fadeOut = 1.5f)
        {
            StopPlaylistInternal();
            if (_current == null || !_current.IsPlaying) return;
            if (fadeOut <= 0f)
            {
                _current.Stop();
                _current = null;
                _fading = null;
                return;
            }
            StartCrossfade(_current, null, fadeOut);
            _current = null;
        }

        void StartCrossfade(MusicChannel outgoing, MusicChannel incoming, float duration)
        {
            if (duration <= 0f)
            {
                if (outgoing != null) outgoing.Stop();
                _fading = null;
                return;
            }

            _fading = outgoing;
            _owner.StartCoroutine(CrossfadeRoutine(outgoing, incoming, duration));
        }

        IEnumerator CrossfadeRoutine(MusicChannel outgoing, MusicChannel incoming, float duration)
        {
            float t = 0f;
            float bgmBusVolume = _owner.BusMixer.GetCurrentVolume(AudioBusKind.Bgm);

            float outgoingStartVol = outgoing != null ? outgoing.TargetVolume : 0f;
            float incomingStartVol = 0f;
            float incomingTargetVol = (incoming != null && incoming.CurrentTrack != null)
                ? incoming.CurrentTrack.DefaultVolume * bgmBusVolume : 0f;

            if (incoming != null) incoming.SetTargetVolume(incomingStartVol);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (outgoing != null)
                    outgoing.SetTargetVolume(Mathf.Lerp(outgoingStartVol, 0f, k));
                if (incoming != null)
                    incoming.SetTargetVolume(Mathf.Lerp(incomingStartVol, incomingTargetVol, k));
                yield return null;
            }

            if (outgoing != null) outgoing.Stop();
            _fading = null;

            if (_activePlaylist != null && incoming != null && incoming == _current)
            {
                _playlistCo = _owner.StartCoroutine(WaitForTrackEndAndAdvance(incoming, _activePlaylist.CrossfadeDuration));
            }
        }

        IEnumerator WaitForTrackEndAndAdvance(MusicChannel channel, float nextCrossfade)
        {
            var track = channel.CurrentTrack;
            if (track == null || track.Clip == null) yield break;

            float length = track.Clip.length;
            float remaining = Mathf.Max(0f, length - track.StartTime);
            float waitTime = remaining - nextCrossfade;
            if (waitTime > 0f) yield return new WaitForSecondsRealtime(waitTime);

            if (_activePlaylist == null || _playlistOrder == null) yield break;
            int next = _playlistCursor + 1;
            PlayPlaylistInternal(next, nextCrossfade);
        }

        public void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
            else Resume();
        }
    }
}