using NUnit.Framework.Interfaces;
using UnityEngine;

public class QuestGiver : MonoBehaviour
{
    [SerializeField] private QuestData quest;
    private bool given;

    private void OnTriggerEnter(Collider other)
    {
        if (given || !other.CompareTag("Player")) return;
        given = true;
        QuestManager.Instance.StartQuest(quest);
    }
}