using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// Global services holder. Creates itself before the first scene loads, so no scene ever
    /// has to contain or wire it up - drop a brand new level scene in and it just works.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        /// <summary>Convenience accessor - stations use this rather than caching a reference.</summary>
        public static Wallet Money => Instance != null ? Instance.Wallet : null;

        public Wallet Wallet { get; private set; }

        [Tooltip("Seconds between background autosaves. Mobile Safari can kill a backgrounded " +
                 "tab without firing any lifecycle event, so we never rely solely on pause/quit.")]
        private const float AutosaveInterval = 15f;

        private float _autosaveTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("~GameRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Wallet = new Wallet();
            SaveSystem.LoadFromDisk(Wallet);

            // 60 fps where the device allows it; on iOS Safari this is a ceiling, not a promise.
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= AutosaveInterval)
            {
                _autosaveTimer = 0f;
                Save();
            }
        }

        public void Save()
        {
            if (Wallet != null) SaveSystem.SaveToDisk(Wallet);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }

        private void OnApplicationQuit() => Save();

        /// <summary>Wipes progress and reloads. Used by the debug panel and by the editor tools.</summary>
        public void ResetProgress()
        {
            SaveSystem.DeleteSave();
            Wallet.SetSilently(0d);
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
