using UnityEngine;

public class QuestHUD : MonoBehaviour
{
    [SerializeField] private float width = 280f;
    [SerializeField] private float margin = 15f;
    [SerializeField] private float lineHeight = 22f;
    [SerializeField] private int minimapHeight = 450;
    

    private GUIStyle titleStyle, questStyle, doneStyle;

    private void InitStyles()
    {
        if (titleStyle != null) return;   

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow }
        };
        questStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        doneStyle = new GUIStyle(questStyle)
        {
            normal = { textColor = Color.green }
        };
    }

    private void OnGUI()
    {
        var qm = QuestManager.Instance;
        if (qm == null || qm.ActiveQuests.Count == 0) return;

        InitStyles();

        // everytask takes 2 lines, so the height is calculated as follows
        float height = 40f + qm.ActiveQuests.Count * lineHeight * 2.2f;
        Rect window = new Rect(Screen.width - width - margin, minimapHeight + margin, width, height);

        GUI.Box(window, GUIContent.none);   

        GUILayout.BeginArea(new Rect(window.x + 10, window.y + 8, window.width - 20, window.height - 16));
        GUILayout.Label("Quest", titleStyle);

        foreach (var q in qm.ActiveQuests)
        {
            var style = q.completed ? doneStyle : questStyle;
            GUILayout.Label(q.data.title, style);

            string progress = q.completed
                ? "  Completed"
                : $"  {q.data.description}  {q.current}/{q.data.requiredAmount}";
            GUILayout.Label(progress, style);
        }
        GUILayout.EndArea();
    }
}