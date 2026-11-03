using Ahoy.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestProgress
{
    public QuestData data;
    public int current;
    public bool completed;
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<QuestProgress> activeQuests = new();
    public IReadOnlyList<QuestProgress> ActiveQuests => activeQuests; // for onGUI display

    public event Action<QuestProgress> OnQuestUpdated;   // for UI 
    public event Action<QuestProgress> OnQuestCompleted;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable() => GameEvents.OnTargetDestroyed += HandleTargetDestroyed;
    private void OnDisable() => GameEvents.OnTargetDestroyed -= HandleTargetDestroyed;

    public void StartQuest(QuestData data)
    {
        activeQuests.Add(new QuestProgress { data = data });
        Debug.Log($"accept mission：{data.title}");
    }

    private void HandleTargetDestroyed(Faction targetFaction)
    {
        foreach (var q in activeQuests)
        {
            if (q.completed || q.data.targetFaction != targetFaction) continue;

            q.current++;
            OnQuestUpdated?.Invoke(q);

            if (q.current >= q.data.requiredAmount)
            {
                q.completed = true;
                OnQuestCompleted?.Invoke(q);
            }
        }
    }
}