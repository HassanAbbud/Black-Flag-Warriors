using Ahoy.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/QuestData")]
public class QuestData : ScriptableObject
{
    public string questId;
    public string title;
    [TextArea] public string description;

    public Faction targetFaction;     
    public int requiredAmount; 
}