using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Ahoy.UI
{

    public abstract class BasePanel : MonoBehaviour
    {
        // Reference to the CanvasGroup component for controlling panel visibility
        private CanvasGroup canvasGroup;
        private float alphaSpeed = 10f;
        private float targetAlpha = 1f;
      

        // Coroutine reference for fading in/out
        private Coroutine fadeCoroutine;

        private UnityAction HideCallBack;

        protected virtual void Awake()
        {
            // Get the CanvasGroup component attached to this GameObject
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
            }
        }

        protected virtual void Start()
        {
            Init();
        }

        public abstract void Init();


        public virtual void Show()
        {
            targetAlpha = 1f;
            StartFade(targetAlpha);
        }


        public virtual void Hide(UnityAction HideCallBack)
        {
            this.HideCallBack = HideCallBack;
            targetAlpha = 0f;
            StartFade(targetAlpha);
        }

        private void StartFade(float targetAlpha)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeTo(targetAlpha));
        }

        private IEnumerator FadeTo(float targetAlpha)
        {
            while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * alphaSpeed);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            if(targetAlpha == 0f) HideCallBack?.Invoke();
            fadeCoroutine = null;
        }
    }

}
