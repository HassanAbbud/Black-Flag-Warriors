using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // Colours a location's banner by who holds it: red for the monsters, blue for the village.
    public sealed class LocationFlag : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] int locationId;
        [SerializeField] Renderer cloth;
        [SerializeField] Color monsterColor = new Color(0.85f, 0.12f, 0.1f);
        [SerializeField] Color villageColor = new Color(0.15f, 0.4f, 0.95f);

        MaterialPropertyBlock block;

        void OnEnable() => BattleEvents.LocationChanged += OnLocationChanged;
        void OnDisable() => BattleEvents.LocationChanged -= OnLocationChanged;

        void Start()
        {
            block = new MaterialPropertyBlock();
            Faction holder = BattleContext.Progress.TryGetLocation(locationId, out BattleLocationStatus status) ? status.Holder : Faction.Monsters;
            Paint(holder);
        }

        void OnLocationChanged(int changedId, Faction holder)
        {
            if (changedId == locationId) Paint(holder);
        }

        void Paint(Faction holder)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, holder == Faction.Village ? villageColor : monsterColor);
            cloth.SetPropertyBlock(block);
        }
    }
}
