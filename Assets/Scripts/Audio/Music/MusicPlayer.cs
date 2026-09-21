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
        Coroutine _crossfadeCo;
        int _transitionVersion;
        bool _paused;
        bool _applicationPaused;
        bool IsPaused => _paused || _applicationPaused || AudioListener.pause;

        public MusicChannel CurrentChannel => _current;
        public BgmTrack CurrentTrack => _current?.CurrentTrack;

        void EnsureChannels()
        {
            if (_channelA == null) _channelA = CreateChannel("MusicA");
            if (_channelB == null) _channelB = CreateChannel("MusicB");
        }

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
            EnsureChannels();
            StopPlaylistInternal();

            if (track == null || track.Clip == null)
            {
                Stop(crossfadeDuration);
                return;
            }

            PlayTrackInternal(track, crossfadeDuration);
        }

        void PlayTrackInternal(BgmTrack track, float crossfadeDuration)
        {
            CancelCrossfade();
            var outgoing = _current != null ? _current : _fading;
            float bgmBusVolume = _owner.BusMixer.GetCurrentVolume(AudioBusKind.Bgm);

            MusicChannel next = (outgoing == _channelA) ? _channelB : _channelA;
            next.Play(track, track.DefaultVolume * bgmBusVolume);

            _current = next;
            if ((_paused || _applicationPaused) && next.Source != null) next.Source.Pause();
            StartCrossfade(outgoing, next, crossfadeDuration);
        }

        public void PlayPlaylist(BgmPlaylist playlist)
        {
            EnsureChannels();
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

            // 最多检查一轮，避免空列表或全空轨道导致递归/死循环。
            for (int checkedCount = 0; checkedCount < _playlistOrder.Length; checkedCount++)
            {
                var track = _activePlaylist.Tracks[_playlistOrder[indexInOrder]];
                if (track != null && track.Clip != null)
                {
                    _playlistCursor = indexInOrder;
                    PlayTrackInternal(track, crossfade);
                    // 播放列表由列表自身推进，忽略单曲的循环设置。
                    if (_current != null && _current.Source != null) _current.Source.loop = false;
                    _playlistCo = _owner.StartCoroutine(WaitForTrackEndAndAdvance(_current, crossfade));
                    return;
                }
                indexInOrder++;
                if (indexInOrder >= _playlistOrder.Length)
                {
                    if (!_activePlaylist.Loop) break;
                    indexInOrder = 0;
                }
            }
            Stop(0f);
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
            _paused = true;
            ApplyPause();
        }

        public void Resume()
        {
            _paused = false;
            ApplyPause();
        }

        void ApplyPause()
        {
            EnsureChannels();
            if (_channelA == null || _channelB == null) return;
            if (_paused || _applicationPaused)
            {
                if (_channelA.Source != null) _channelA.Source.Pause();
                if (_channelB.Source != null) _channelB.Source.Pause();
            }
            else
            {
                if (_channelA.Source != null) _channelA.Source.UnPause();
                if (_channelB.Source != null) _channelB.Source.UnPause();
            }
        }

        void CancelCrossfade()
        {
            _transitionVersion++;
            if (_crossfadeCo != null) _owner.StopCoroutine(_crossfadeCo);
            _crossfadeCo = null;
        }

        public void Stop(float fadeOut = 1.5f)
        {
            EnsureChannels();
            StopPlaylistInternal();
            CancelCrossfade();
            var outgoing = _current != null ? _current : _fading;
            var other = outgoing == _channelA ? _channelB : _channelA;
            other.Stop();
            _current = null;
            if (fadeOut <= 0f || outgoing == null)
            {
                _channelA.Stop();
                _channelB.Stop();
                _fading = null;
                return;
            }
            StartCrossfade(outgoing, null, fadeOut);
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
            _crossfadeCo = _owner.StartCoroutine(CrossfadeRoutine(outgoing, incoming, duration, _transitionVersion));
        }

        IEnumerator CrossfadeRoutine(MusicChannel outgoing, MusicChannel incoming, float duration, int version)
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
                if (version != _transitionVersion) yield break;
                if (IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (outgoing != null)
                    outgoing.SetTargetVolume(Mathf.Lerp(outgoingStartVol, 0f, k));
                if (incoming != null)
                    incoming.SetTargetVolume(Mathf.Lerp(incomingStartVol, incomingTargetVol, k));
                yield return null;
            }

            if (version != _transitionVersion) yield break;
            if (outgoing != null) outgoing.Stop();
            _fading = null;
            _crossfadeCo = null;
        }

        IEnumerator WaitForTrackEndAndAdvance(MusicChannel channel, float nextCrossfade)
        {
            var track = channel.CurrentTrack;
            if (track == null || track.Clip == null) yield break;

            // 从实际播放进度判断，暂停不推进，也不重复计算淡入时间。
            int version = _transitionVersion;
            if (channel == null || channel.Source == null) yield break;
            float leadTime = Mathf.Clamp(nextCrossfade, 0f, (track.Clip.length - channel.Source.time) * 0.5f);
            yield return null;
            while (_activePlaylist != null && channel == _current && version == _transitionVersion)
            {
                if (!IsPaused && (channel.Source.time >= track.Clip.length - leadTime
                                  || !channel.IsPlaying))
                    break;
                yield return null;
            }
            if (_activePlaylist == null || channel != _current || version != _transitionVersion) yield break;
            _playlistCo = null;
            PlayPlaylistInternal(_playlistCursor + 1, nextCrossfade);
        }

        public void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            ApplyPause();
        }
    }
}
