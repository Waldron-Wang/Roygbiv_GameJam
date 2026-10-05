using System.Collections;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Owns the save data and the high-level game flow:
    ///   Menu -> Hub -> Level -> (reclaim sequence) -> Hub ... -> Ending.
    /// The ONLY system that writes to GameProgress.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameProgress Progress { get; private set; }

        void Awake() => Progress = GameProgress.Load();

        void OnEnable()
        {
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.LevelFailed += OnLevelFailed;
        }

        void OnDisable()
        {
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.LevelFailed -= OnLevelFailed;
        }

        // ---------- Commands (called by menus / hub / cheats) ----------

        public void NewGame()
        {
            GameProgress.DeleteSave();
            Progress = new GameProgress();
            Game.Colors.SyncWithProgress();
            Game.Instructions.ForgetShown(); // a new run gets every pre-boss card again
            Game.Scenes.Load(Game.Config.hubScene);
        }

        public void ContinueGame() => Game.Scenes.Load(Game.Config.hubScene);
        public void ReturnToMenu() => Game.Scenes.Load(Game.Config.mainMenuScene);
        public void ReturnToHub() => Game.Scenes.Load(Game.Config.hubScene);

        public void EnterLevel(ColorId color)
        {
            var data = Game.Config.Get(color);
            if (data == null) { Debug.LogError($"No ColorData for {color} in GameConfig."); return; }
            Game.Scenes.Load(data.sceneName);
        }

        /// <summary>Linear unlock: a color is playable once every color before it (in play order) is restored.</summary>
        public bool IsUnlocked(ColorId color)
        {
            var order = Game.Config.colorOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].id == color) return true;
                if (!Progress.IsRestored(order[i].id)) return false;
            }
            return false;
        }

        public bool AllColorsRestored => Progress.RestoredCount >= Game.Config.colorOrder.Count;

        /// <summary>Restore a color + grant its ability. Used by the level flow and by debug cheats.</summary>
        public void RestoreColor(ColorId color)
        {
            if (Progress.IsRestored(color)) return;
            var data = Game.Config.Get(color);
            Progress.Restore(color);
            if (data != null) Progress.Unlock(data.grantedAbility);
            Progress.Save();

            GameEvents.RaiseColorRestored(color);
            if (data != null && data.grantedAbility != AbilityId.None)
                GameEvents.RaiseAbilityUnlocked(data.grantedAbility);
        }

        // ---------- Event handlers ----------

        void OnLevelCompleted(ColorId color) => StartCoroutine(ReclaimSequence(color));

        void OnLevelFailed(ColorId color) => StartCoroutine(RespawnSequence());

        // Core loop step 3 + 4: reclaim the color, recolor the world, story fragment, move on.
        IEnumerator ReclaimSequence(ColorId color)
        {
            bool firstTime = !Progress.IsRestored(color);
            RestoreColor(color);

            var data = Game.Config.Get(color);
            if (firstTime && data != null && data.storyFragment != null)
                yield return Game.Dialogue.Play(data.storyFragment);

            yield return new WaitForSeconds(Game.Config.returnToHubDelay);
            Game.Scenes.Load(AllColorsRestored ? Game.Config.endingScene : Game.Config.hubScene);
        }

        IEnumerator RespawnSequence()
        {
            yield return new WaitForSeconds(Game.Config.respawnDelay);
            Game.Scenes.Reload();
        }
    }
}
