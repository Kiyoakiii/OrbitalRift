using UnityEngine;
namespace OrbitalRift {
[CreateAssetMenu(menuName="Orbital Rift/Gameplay/Player ship")]
public sealed class ShipDefinition:ScriptableObject {
public ShipArchetype Archetype; public string DisplayName;
public DamageElement Element;
[Min(.01f)] public float FireIntervalMultiplier=1, ProjectileSpeedMultiplier=1, DamageMultiplier=1;
public Color ProjectileColor=Color.white;
public ShipLoadout Loadout=>new ShipLoadout(Archetype,Element,FireIntervalMultiplier,ProjectileSpeedMultiplier,DamageMultiplier,ProjectileColor);
}}
