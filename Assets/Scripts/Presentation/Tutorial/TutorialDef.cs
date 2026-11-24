using System;
using UnityEngine;

namespace Ahoy.Presentation
{
    // What can open a tutorial page. Appended last only, so stored assets keep their meaning.
    public enum TutorialTrigger
    {
        GameStart,          // the level has loaded
        QuestAccepted,      // TutorialQuestCue: Zahim reached a quest NPC
        BossSpawned,        // a portal camp entered PortalPhase.SpawnBoss
        WeakPointRevealed,  // a boss's red diamond showed for the first time
        BossDefeated,       // a portal camp entered PortalPhase.Sealed after its boss came out
    }

    // The tutorial's words and pacing (Code Patterns 2.3: data, not code). TutorialDirector shows
    // each page once, the first time its trigger happens; pages sharing a trigger play in order.
    [CreateAssetMenu(menuName = "Ahoy/Tutorial Def", fileName = "TUT_")]
    public sealed class TutorialDef : ScriptableObject
    {
        [Serializable]
        public struct Page
        {
            public TutorialTrigger trigger;
            public string title;
            [Tooltip("TextMeshPro rich text is allowed: <b>, <i>, <color=#hex>.")]
            [TextArea(6, 20)] public string body;
        }

        public Page[] pages;

        [Tooltip("Line under every page telling the player how to close it.")]
        public string continuePrompt = "Press any button to continue";

        [Tooltip("Real-time seconds a page stays up before a press can close it, so a player mashing attack does not skip it unread.")]
        public float minShowSeconds = 0.6f;

        [Tooltip("Real-time seconds the panel takes to fade in.")]
        public float fadeInSeconds = 0.25f;
    }
}
