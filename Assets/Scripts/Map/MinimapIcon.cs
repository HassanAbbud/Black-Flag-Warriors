using UnityEngine;
using UnityEngine.UI;

public class MinimapIcon : MonoBehaviour
{
    public Image body;
    public Image battleSwords;
    public float flashSpeed = 3f;
    public Color flashColor = Color.white;

    public void Bind(MinimapMarker m, Sprite sprite)
    {
        body.sprite = sprite;
        body.color = m.color;
        body.rectTransform.localScale = Vector3.one;
        battleSwords.gameObject.SetActive(false);
    }

    public void Tick(MinimapMarker m)
    {
        if (!m.InBattle)
        {
            body.color = m.color;
            body.rectTransform.localScale = Vector3.one;
            if (battleSwords.gameObject.activeSelf)
                battleSwords.gameObject.SetActive(false);
            return;
        }

        // In battle: flash the icon and show swords
        float t = Mathf.PingPong(Time.unscaledTime * flashSpeed, 1f);
        body.color = Color.Lerp(m.color, flashColor, t);
        body.rectTransform.localScale = Vector3.one * (1f + 0.15f * t);

        if (!battleSwords.gameObject.activeSelf)
            battleSwords.gameObject.SetActive(true);
        battleSwords.rectTransform.localScale = Vector3.one * (1f + 0.25f * t);
    }
}