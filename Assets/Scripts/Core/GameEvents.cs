using System;
using UnityEngine;

namespace Ahoy.Core
{
    public static class GameEvents
    {
        // Enemy is destroyed, broadcast the target faction
        public static event Action<Faction> OnTargetDestroyed;
        public static void TargetDestroyed(Faction targetFaction) => OnTargetDestroyed?.Invoke(targetFaction);

        // Player enters the quest area, broadcast the quest ID (e.g., "Quest_001")
        public static event Action<string> OnQuestTriggered;
        public static void QuestTriggered(string questId) => OnQuestTriggered?.Invoke(questId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnTargetDestroyed = null;
            OnQuestTriggered = null;
        }
    }
}