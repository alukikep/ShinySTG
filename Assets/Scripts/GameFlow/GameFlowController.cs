using System;
using System.Collections;
using System.Collections.Generic;
using ShinySTG.Audio;
using ShinySTG.Player;
using ShinySTG.UI;
using ShinySTG.Level;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShinySTG.GameFlow
{
    /// <summary>只保留本次开局参数与加载画布，战斗对象随 Single 场景卸载。</summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }
        public bool IsLoading { get; private set; }
        public GameStartRequest CurrentRequest { get; private set; }
        public RunSession CurrentSession { get; private set; }
        public string Failure { get; private set; }
        public bool IsShowingResults { get; private set; }
        public bool IsShowingStageResults => _stageResultSession != null;

        StageResultView _stageResultView;
        RunSession _stageResultSession;
        GameplayBootstrap _stageResultBootstrap;
        int _stageResultAttempt;
        readonly KeyboardMenuNavigation _stageResultNavigation = new();
        bool _focused = true;
        ScreenWipeTransition _transition;
        GameplayBootstrap _bootstrap;
        IDisposable _stageRestriction;
        Vector2 _resultsScroll;
        int _resultSelection;
        bool _resultsInputReady;
        IDisposable _controlLock;
        string _menuScenePath = "Assets/Scenes/StartMenu.unity";
        float _previousTimeScale = 1f;
        bool _ownsTimeScale;

        public static GameFlowController EnsureInstance()
        {
            if (Instance == null) new GameObject("GameFlow").AddComponent<GameFlowController>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var canvasObject = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var curtain = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
            curtain.transform.SetParent(canvasObject.transform, false);
            var image = curtain.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            _transition = canvasObject.AddComponent<ScreenWipeTransition>();
            _transition.Configure(image);
            _transition.ResetTransition();
            var resultsObject = new GameObject("StageResults", typeof(RectTransform));
            resultsObject.transform.SetParent(transform, false);
            _stageResultView = resultsObject.AddComponent<StageResultView>();
        }

        public bool TryStartGame(CharacterDefinition character, StageDefinition stage, out string error)
            => TryStartRequest(new GameStartRequest(character, stage), out error);

        public bool TryStartSequence(CharacterDefinition character, StageSequenceDefinition sequence, out string error)
            => TryStartRequest(GameStartRequest.FromSequence(character, sequence), out error);

        bool TryStartRequest(GameStartRequest request, out string error)
        {
            error = null;
            if (IsLoading || Failure != null) { error = "正在处理场景切换。"; return false; }
            if (!request.Validate(out error)) return false;
            var stage = request.Stage;
            if (!Application.CanStreamedLevelBeLoaded(stage.ScenePath))
            {
                error = "目标场景未加入 Build Settings：" + stage.ScenePath;
                return false;
            }
            _menuScenePath = SceneManager.GetActiveScene().path;
            BeginOperation(request);
            StartCoroutine(RunGuarded(LoadGame()));
            return true;
        }

        public void StartInPlace(GameplayBootstrap bootstrap, GameStartRequest request)
        {
            if (IsLoading || Failure != null) return;
            BeginOperation(request);
            StartCoroutine(RunGuarded(InitializeInPlace(bootstrap)));
        }

        void BeginOperation(GameStartRequest request)
        {
            HideStageResults();
            _bootstrap?.PauseMenu?.CloseForTransition();
            IsShowingResults = false;
            _bootstrap = null;
            ReleaseControl();
            ReleaseStageRestriction();
            IsLoading = true;
            Failure = null;
            CurrentRequest = request;
            CurrentSession = request != null && request.Validate(out _) ? new RunSession(request) : null;
            _previousTimeScale = Time.timeScale;
            _ownsTimeScale = true;
            // 初始化期间不消耗出生无敌、背景时间或关卡时间；黑幕使用 unscaledDeltaTime。
            Time.timeScale = 0f;
            _controlLock = PlayerControlLock.Acquire();
        }

        IEnumerator LoadGame()
        {
            yield return _transition.Cover();
            ResetAudio();
            var operation = SceneManager.LoadSceneAsync(CurrentRequest.Stage.ScenePath, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("无法创建场景加载请求。");
            yield return operation;
            // 场景激活本身可能触发大量 Awake/OnEnable。先让 Unity 完成一次渲染循环，
            // 再查找 Bootstrap，避免把场景激活、对象扫描和玩家实例化压在同一帧。
            yield return null;
            var scene = SceneManager.GetActiveScene();
            GameplayBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<GameplayBootstrap>(true))
            {
                if (!candidate.isActiveAndEnabled) continue;
                if (bootstrap != null) throw new InvalidOperationException("游戏场景包含多个 GameplayBootstrap。");
                bootstrap = candidate;
            }
            if (bootstrap == null) throw new InvalidOperationException("目标场景缺少启用的 GameplayBootstrap。");
            yield return Initialize(bootstrap);
        }

        IEnumerator InitializeInPlace(GameplayBootstrap bootstrap)
        {
            yield return _transition.Cover();
            ResetAudio();
            yield return Initialize(bootstrap);
        }

        IEnumerator Initialize(GameplayBootstrap bootstrap)
        {
            if (CurrentRequest == null || !CurrentRequest.Validate(out _))
                throw new InvalidOperationException("请配置默认角色和关卡，或从开始菜单进入。");

            // 玩家 prefab 的 Instantiate 会同步执行 Awake/OnEnable。拆出一帧让加载黑幕
            // 先稳定显示，减少从角色选择进入关卡时的可感知卡顿。
            yield return null;
            bootstrap.Prepare(CurrentRequest);
            _bootstrap = bootstrap;
            // 动态生成对象的 Start 和 HUD 的 LateUpdate 在揭幕前完成；额外让一帧
            // 消化首次初始化、Canvas rebuild 和资源上传。
            yield return null;
            yield return null;
            bootstrap.Begin();
            // BeginLevel 可能触发首批敌人、背景和 HUD 的初始化。必须在黑幕仍覆盖时
            // 先完成这些工作，否则第一次揭幕帧会把初始化尖峰暴露成明显卡顿。
            yield return null;
            yield return null;
            yield return _transition.Reveal();
            Time.timeScale = 1f;
            _ownsTimeScale = false;
            ReleaseControl();
            IsLoading = false;
        }

        bool IsCurrentClear(GameplayBootstrap bootstrap, RunSession session, int attempt)
            => bootstrap != null && bootstrap == _bootstrap && session == CurrentSession
                && bootstrap.Level != null && bootstrap.Level.AttemptId == attempt
                && bootstrap.Level.EndReason == LevelEndReason.Cleared;

        IEnumerator FinishStage(GameplayBootstrap bootstrap, RunSession session, int attempt)
        {
            try
            {
                // 结束事件仍在派发、回池仍可能待处理；至少跨一帧再消费结算。
                yield return null;
                while (IsCurrentClear(bootstrap, session, attempt) && bootstrap.Level.IsBattleCleanupPending)
                    yield return null;
                if (!IsCurrentClear(bootstrap, session, attempt)) yield break;
                if (!session.IsCurrentStageRecorded)
                    throw new InvalidOperationException("通关结果未记录，无法推进关卡。");

                // 同帧掉命但仍有残机时，等待复活完成，避免把死亡状态带到下一关。
                while (bootstrap.SpawnedPlayer != null && bootstrap.SpawnedPlayer.Health.IsDying)
                {
                    if (!IsCurrentClear(bootstrap, session, attempt)) yield break;
                    if (bootstrap.SpawnedPlayer.Health.IsDead)
                        throw new InvalidOperationException("通关后的玩家已死亡。");
                    yield return null;
                }
                if (bootstrap.SpawnedPlayer == null || bootstrap.SpawnedPlayer.Health.IsDead)
                    throw new InvalidOperationException("通关后的玩家不可用。");
                if (!IsCurrentClear(bootstrap, session, attempt)) yield break;
                _controlLock ??= PlayerControlLock.Acquire();
                _stageResultBootstrap = bootstrap;
                _stageResultSession = session;
                _stageResultAttempt = attempt;
                _stageResultNavigation.Reset();
                _stageResultView.Show(session.Results[session.CurrentStageIndex], session.IsComplete);
            }
            finally { IsLoading = false; }
        }

        IEnumerator AdvanceAfterStageResult(GameplayBootstrap bootstrap, RunSession session, int attempt)
        {
            StageFadePrototype fade = null;
            try
            {
                if (!IsCurrentClear(bootstrap, session, attempt)) yield break;
                if (session.IsComplete)
                {
                    IsShowingResults = true;
                    _resultsInputReady = false;
                    _resultSelection = 0;
                    _resultsScroll = Vector2.zero;
                    yield break;
                }

                _stageRestriction = BattleRestriction.Acquire();
                foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<StageFadePrototype>(false))
                    if (fade == null && candidate.TryBeginFlow()) fade = candidate;
                yield return fade != null ? fade.FadeForFlow(true) : _transition.Fade(true);
                if (!IsCurrentClear(bootstrap, session, attempt)) yield break;
                if (!CurrentRequest.Validate(out var error)) throw new InvalidOperationException(error);
                ResetAudio();
                if (!session.TryAdvanceStage()) throw new InvalidOperationException("无法推进关卡序列。");
                bootstrap.PrepareNextStage(session.CurrentStage);
                bootstrap.Level.BeginLevel();
                if (!bootstrap.Level.IsRunning) throw new InvalidOperationException("下一关启动失败。");
                int nextAttempt = bootstrap.Level.AttemptId;
                yield return null;
                yield return fade != null ? fade.FadeForFlow(false) : _transition.Fade(false);
                if (bootstrap == null || bootstrap.Level == null || bootstrap.Level.AttemptId != nextAttempt
                    || !bootstrap.Level.IsRunning)
                    throw new InvalidOperationException("换关期间关卡被重置。");
            }
            finally
            {
                if (fade != null) fade.Cancel();
                _transition.ResetTransition();
                // 异常时维持限制，交由返回标题或销毁释放。
                if (Failure == null)
                {
                    ReleaseStageRestriction();
                    if (!IsShowingResults) ReleaseControl();
                }
                IsLoading = false;
            }
        }

        public void RestartRun()
        {
            if (IsLoading || (!IsShowingResults && !(_bootstrap != null && _bootstrap.PauseMenu != null
                && _bootstrap.PauseMenu.IsOpen)) || CurrentRequest == null) return;
            if (!CurrentRequest.Validate(out var error)) { Failure = error; return; }
            if (!Application.CanStreamedLevelBeLoaded(CurrentRequest.Stage.ScenePath))
            {
                Failure = "目标场景未加入 Build Settings：" + CurrentRequest.Stage.ScenePath;
                return;
            }
            // 使用本局固定的角色与关卡顺序，重新加载以彻底重置玩家资源。
            _bootstrap?.Level?.EndLevel(LevelEndReason.Aborted);
            BeginOperation(CurrentRequest);
            StartCoroutine(RunGuarded(LoadGame()));
        }

        static void ResetAudio()
        {
            var audio = AudioSystem.Instance;
            if (audio == null) return;
            audio.EventHub?.DisableAutoSwitch();
            audio.EventHub?.SwitchToSilence(0f);
            audio.Sfx?.StopAll();
        }

        // 展开嵌套 IEnumerator，使初始化/过渡异常也进入可返回菜单的失败状态。
        IEnumerator RunGuarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    object next = null;
                    Exception failure = null;
                    bool moved = false;
                    try
                    {
                        moved = stack.Peek().MoveNext();
                        if (moved) next = stack.Peek().Current;
                    }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null)
                    {
                        Failure = failure.Message;
                        HideStageResults();
                        _stageRestriction ??= BattleRestriction.Acquire();
                        IsLoading = false;
                        Debug.LogException(failure, this);
                        yield break;
                    }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (next is IEnumerator nested) stack.Push(nested);
                    else yield return next;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                if (Failure != null) _transition.ResetTransition();
            }
        }

        public void ReturnToMenu()
        {
            if (IsLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(_menuScenePath))
            {
                Failure = "开始场景未加入 Build Settings，请停止播放后运行 STG/Game Flow/Setup First Playable。";
                return;
            }
            IsLoading = true;
            Failure = null;
            StartCoroutine(RunGuarded(LoadMenu()));
        }

        IEnumerator LoadMenu()
        {
            HideStageResults();
            _bootstrap?.PauseMenu?.CloseForTransition();
            IsShowingResults = false;
            _bootstrap = null;
            ShinySTG.Level.LevelController.Instance?.EndLevel(ShinySTG.Level.LevelEndReason.Aborted);
            yield return _transition.Cover();
            ResetAudio();
            CurrentRequest = null;
            CurrentSession = null;
            yield return SceneManager.LoadSceneAsync(_menuScenePath, LoadSceneMode.Single);
            yield return _transition.Reveal();
            Time.timeScale = 1f;
            _ownsTimeScale = false;
            ReleaseControl();
            ReleaseStageRestriction();
            IsLoading = false;
        }

        void OnGUI()
        {
            if (Failure == null && IsShowingResults && !IsLoading)
            {
                DrawResults();
                return;
            }
            if (Failure == null) return;
            GUI.depth = -1000;
            var area = new Rect((Screen.width - 560f) / 2f, (Screen.height - 200f) / 2f, 560f, 200f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Unable to start game / 开局失败");
            GUILayout.Label(Failure);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Return to title / 返回标题")) ReturnToMenu();
            GUILayout.EndArea();
        }

        void Update()
        {
            if (IsShowingStageResults && !IsCurrentClear(_stageResultBootstrap, _stageResultSession, _stageResultAttempt))
            {
                HideStageResults();
                ReleaseControl();
            }
            if (!IsLoading && Failure == null && !IsShowingResults && !IsShowingStageResults && _bootstrap != null
                && CurrentSession != null && _bootstrap.Level != null
                && _bootstrap.Level.EndReason == LevelEndReason.Cleared)
            {
                IsLoading = true;
                StartCoroutine(RunGuarded(FinishStage(_bootstrap, CurrentSession, _bootstrap.Level.AttemptId)));
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!_focused) return;
            if (Failure == null && IsShowingStageResults && !IsLoading)
            {
                _stageResultNavigation.Read(false, false, Input.GetKey(KeyCode.Z), Input.GetKeyDown(KeyCode.Z),
                    false, false, Time.unscaledTime, .35f, .1f, out _, out bool confirm, out _);
                if (confirm) ConfirmStageResult();
                return;
            }
            if (Failure != null && !IsLoading && Input.GetKeyDown(KeyCode.Escape)) ReturnToMenu();
            if (Failure == null && IsShowingResults && !IsLoading)
            {
                if (!_resultsInputReady)
                {
                    _resultsInputReady = !Input.GetKey(KeyCode.Z) && !Input.GetKey(KeyCode.Return)
                        && !Input.GetKey(KeyCode.Escape);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    _resultSelection = 1 - _resultSelection;
                if (Input.GetKeyDown(KeyCode.Escape)) ReturnToMenu();
                else if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
                {
                    if (_resultSelection == 0) RestartRun(); else ReturnToMenu();
                }
            }
#endif
        }

        void DrawResults()
        {
            GUI.depth = -900;
            float width = Mathf.Min(620f, Screen.width - 24f);
            float height = Mathf.Min(520f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f,
                width, height), GUI.skin.box);
            GUILayout.Label("ALL CLEAR / 全关通关", new GUIStyle(GUI.skin.label)
                { fontSize = 26, alignment = TextAnchor.MiddleCenter });
            var results = CurrentSession.Results;
            GUILayout.Label($"TOTAL SCORE / 总分    {results[results.Count - 1].TotalScore:N0}");
            _resultsScroll = GUILayout.BeginScrollView(_resultsScroll);
            foreach (var result in results)
                GUILayout.Label($"STAGE {result.StageIndex + 1}  {result.StageId}     {result.Score:N0}");
            GUILayout.EndScrollView();
            if (GUILayout.Button((_resultSelection == 0 ? "> " : "") + "Restart run / 重新开始本局", GUILayout.Height(40)))
                RestartRun();
            if (GUILayout.Button((_resultSelection == 1 ? "> " : "") + "Return to title / 返回标题", GUILayout.Height(40)))
                ReturnToMenu();
            GUILayout.Label("↑ / ↓   Z / Enter     Esc: Title");
            GUILayout.EndArea();
        }

        void ConfirmStageResult()
        {
            if (!IsShowingStageResults || IsLoading || Failure != null) return;
            var bootstrap = _stageResultBootstrap;
            var session = _stageResultSession;
            int attempt = _stageResultAttempt;
            HideStageResults();
            if (!IsCurrentClear(bootstrap, session, attempt)) { ReleaseControl(); return; }
            IsLoading = true;
            StartCoroutine(RunGuarded(AdvanceAfterStageResult(bootstrap, session, attempt)));
        }

        void HideStageResults()
        {
            if (_stageResultView != null) _stageResultView.Hide();
            _stageResultSession = null;
            _stageResultBootstrap = null;
            _stageResultNavigation.Reset();
        }

        void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            _stageResultNavigation.Reset();
            _resultsInputReady = false;
        }

        void ReleaseControl() { _controlLock?.Dispose(); _controlLock = null; }
        void ReleaseStageRestriction() { _stageRestriction?.Dispose(); _stageRestriction = null; }

        void OnDestroy()
        {
            if (Instance != this) return;
            StopAllCoroutines();
            HideStageResults();
            ReleaseControl();
            ReleaseStageRestriction();
            if (_ownsTimeScale) Time.timeScale = _previousTimeScale;
            Instance = null;
        }
    }
}
