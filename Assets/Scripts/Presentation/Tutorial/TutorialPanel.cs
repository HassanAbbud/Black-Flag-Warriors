using TMPro;
using UnityEngine;

namespace Ahoy.Presentation
{
    // The see-through card in the middle of the screen. Only displays what TutorialDirector hands
    // it; it never decides when a page shows. Fades on unscaled time because the game is paused.
    public sealed class TutorialPanel : MonoBehaviour
    {
        [Tooltip("A child holding the whole card. Not this object, or the panel stops updating.")]
        [SerializeField] GameObject root;
        [Tooltip("On the root. Its alpha fades the card in; the background Image's own alpha sets how see-through it is.")]
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text prompt;

        float fadeSeconds;
        float shownAt;

        public bool IsShowing => root.activeSelf;

        void Awake() => Hide();

        public void Show(string pageTitle, string pageBody, string continuePrompt, float fadeInSeconds)
        {
            title.text = pageTitle;
            body.text = pageBody;
            prompt.text = continuePrompt;
            prompt.enabled = false;   // appears once a press may close the page
            fadeSeconds = fadeInSeconds;
            shownAt = Time.unscaledTime;
            group.alpha = fadeSeconds > 0f ? 0f : 1f;
            root.SetActive(true);
        }

        public void ShowPrompt() => prompt.enabled = true;

        public void Hide() => root.SetActive(false);

        void Update()
        {
            if (!root.activeSelf || group.alpha >= 1f) return;
            group.alpha = Mathf.Clamp01((Time.unscaledTime - shownAt) / fadeSeconds);
        }
    }
}
