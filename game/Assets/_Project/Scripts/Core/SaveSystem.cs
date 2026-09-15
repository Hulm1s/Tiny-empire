using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// Whole-game persistence.
    ///
    /// The saved blob is the master record, not the loaded scene. Live components overwrite
    /// their own entries on save and every other entry is preserved untouched, so a level that
    /// is currently unloaded (the player is off running the bakery) keeps its state intact.
    /// </summary>
    public static class SaveSystem
    {
        private const string PrefsKey = "tycoon.save.v1";

        [Serializable]
        private class Blob
        {
            public double money;
            public double savedAtUnix;
            public List<string> keys = new List<string>();
            public List<string> values = new List<string>();
        }

        private static readonly Dictionary<string, string> Stored = new Dictionary<string, string>();
        private static readonly List<ISaveable> Live = new List<ISaveable>();

        private static bool _loaded;
        private static double _pendingMoney;

        /// <summary>
        /// True while progress is being wiped, from the moment the save is deleted until the
        /// fresh scene has finished loading.
        ///
        /// Deleting the save clears the stored blob, but reloading the scene then destroys
        /// every saveable in the old one - and unregistering captures state on the way out,
        /// which put all the old progress straight back. Purchases the player had partly paid
        /// off survived a wipe that had, as far as the player could tell, just happened.
        /// </summary>
        private static bool _wiping;

        /// <summary>Seconds that elapsed while the game was closed, clamped. Zero on a fresh save.</summary>
        public static double OfflineSeconds { get; private set; }

        public static bool HasSave { get; private set; }

        public static void LoadFromDisk(Wallet wallet)
        {
            Stored.Clear();
            OfflineSeconds = 0d;
            HasSave = false;

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var blob = JsonUtility.FromJson<Blob>(json);
                    if (blob != null)
                    {
                        int count = Mathf.Min(blob.keys.Count, blob.values.Count);
                        for (int i = 0; i < count; i++) Stored[blob.keys[i]] = blob.values[i];

                        _pendingMoney = blob.money;
                        OfflineSeconds = GameClock.ClampOffline(GameClock.NowUnix - blob.savedAtUnix);
                        HasSave = true;
                    }
                }
                catch (Exception e)
                {
                    // A corrupt save must never brick the game on someone's phone.
                    Debug.LogWarning($"[SaveSystem] Save file unreadable, starting fresh. {e.Message}");
                    Stored.Clear();
                    HasSave = false;
                }
            }

            _loaded = true;
            wallet.SetSilently(HasSave ? _pendingMoney : 0d);
        }

        /// <summary>
        /// Components call this from OnEnable. If saved state exists it is restored immediately,
        /// which means additively loaded levels restore themselves with no extra plumbing.
        /// </summary>
        public static void Register(ISaveable saveable)
        {
            if (saveable == null || Live.Contains(saveable)) return;
            Live.Add(saveable);

            if (_loaded && Stored.TryGetValue(saveable.SaveKey, out string json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    saveable.RestoreState(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Could not restore '{saveable.SaveKey}': {e.Message}");
                }
            }
        }

        public static void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            // Capture on the way out so unloading a level does not lose its progress - unless
            // the progress is exactly what is being thrown away.
            if (!_wiping) CaptureOne(saveable);
            Live.Remove(saveable);
        }

        private static void CaptureOne(ISaveable saveable)
        {
            try
            {
                string json = saveable.CaptureState();
                if (!string.IsNullOrEmpty(json)) Stored[saveable.SaveKey] = json;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not capture '{saveable.SaveKey}': {e.Message}");
            }
        }

        public static void SaveToDisk(Wallet wallet)
        {
            for (int i = 0; i < Live.Count; i++) CaptureOne(Live[i]);

            var blob = new Blob
            {
                money = wallet.Balance,
                savedAtUnix = GameClock.NowUnix
            };
            foreach (var pair in Stored)
            {
                blob.keys.Add(pair.Key);
                blob.values.Add(pair.Value);
            }

            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(blob));
            // Required on the Web platform: this is what flushes the browser's IndexedDB.
            PlayerPrefs.Save();
        }

        public static void DeleteSave()
        {
            Stored.Clear();
            Live.Clear();
            _pendingMoney = 0d;
            OfflineSeconds = 0d;
            HasSave = false;
            _wiping = true;

            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Called once the replacement scene is up, to start recording again.
        /// Until this runs, nothing that unloads writes anything back.
        /// </summary>
        public static void FinishWipe() => _wiping = false;
    }
}
