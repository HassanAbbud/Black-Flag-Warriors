using UnityEngine;

namespace Ahoy.Presentation
{
    // Sits beside a QuestGiver on the quest NPC and hears the same trigger collider, so the
    // tutorial learns a quest was accepted without the quest scripts knowing the tutorial exists.
    // QuestGiver hands out its quest the first time the Player-tagged object enters; so does this.
    public sealed class TutorialQuestCue : MonoBehaviour
    {
        [SerializeField] TutorialDirector director;
        [Tooltip("Must match the tag QuestGiver checks for.")]
        [SerializeField] string playerTag = "Player";

        bool cued;

        void Awake() => Debug.Assert(director != null, "TutorialQuestCue needs the scene's TutorialDirector", this);

        void OnTriggerEnter(Collider other)
        {
            if (cued || director == null || !other.CompareTag(playerTag)) return;
            cued = true;
            director.Notify(TutorialTrigger.QuestAccepted);
        }
    }
}
