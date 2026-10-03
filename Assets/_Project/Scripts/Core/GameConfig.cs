using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Global, designer-tunable settings. Lives at Assets/_Project/Resources/GameConfig.asset
    /// and is loaded automatically by the Bootstrapper. Access it via Game.Config.
    /// </summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Tooltip("Play order. A color unlocks once every color before it is restored.")]
        public List<ColorData> colorOrder = new();

        [Header("Scenes")]
        public string mainMenuScene = "MainMenu";
        public string hubScene = "Hub";
        public string endingScene = "Ending";

        [Header("Flow")]
        [Tooltip("Seconds to admire the recolored world before returning to the hub.")]
        public float returnToHubDelay = 2.5f;
        public float recolorDuration = 2f;
        public float respawnDelay = 1.5f;

        public ColorData Get(ColorId id) => colorOrder.Find(c => c != null && c.id == id);
        public int IndexOf(ColorId id) => colorOrder.FindIndex(c => c != null && c.id == id);
    }
}
