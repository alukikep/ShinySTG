using System;
using System.Collections;
using System.Collections.Generic;
using ShinySTG.Audio;
using ShinySTG.Player;
using ShinySTG.UI;
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
        public string Failure { get; private set; }

        ScreenWipeTransition _transition;
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
        }

        public bool TryStartGame(CharacterDefinition character, StageDefinition stage, out string error)
        {
            error = null;
            if (IsLoading || Failure != null) { error = "正在处理场景切换。"; return false; }
            var request = new GameStartRequest(character, stage);
            if (!request.Validate(out error)) return false;
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
            IsLoading = true;
            Failure = null;
            CurrentRequest = request;
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
            // 动态生成对象的 Start 和 HUD 的 LateUpdate 在揭幕前完成；额外让一帧
            // 消化首次初始化、Canvas rebuild 和资源上传。
            yield return null;
            yield return null;
            yield return _transition.Reveal();
            bootstrap.Begin();
            Time.timeScale = 1f;
            _ownsTimeScale = false;
            ReleaseControl();
            IsLoading = false;
        }

        static void ResetAudio()
        {
            var audio = AudioSystem.Instance;
            audio?.EventHub?.DisableAutoSwitch();
            audio?.EventHub?.SwitchToSilence(0f);
            audio?.Sfx?.StopAll();
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
            yield return _transition.Cover();
            ResetAudio();
            CurrentRequest = null;
            yield return SceneManager.LoadSceneAsync(_menuScenePath, LoadSceneMode.Single);
            yield return _transition.Reveal();
            Time.timeScale = 1f;
            _ownsTimeScale = false;
            ReleaseControl();
            IsLoading = false;
        }

        void OnGUI()
        {
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
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Failure != null && !IsLoading && Input.GetKeyDown(KeyCode.Escape)) ReturnToMenu();
#endif
        }

        void ReleaseControl() { _controlLock?.Dispose(); _controlLock = null; }

        void OnDestroy()
        {
            if (Instance != this) return;
            StopAllCoroutines();
            ReleaseControl();
            if (_ownsTimeScale) Time.timeScale = _previousTimeScale;
            Instance = null;
        }
    }
}
