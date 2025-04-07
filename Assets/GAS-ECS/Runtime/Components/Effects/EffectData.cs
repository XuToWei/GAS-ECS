using UnityEngine;
using System;

namespace GAS.Effects
{
    [CreateAssetMenu(fileName = "New Effect", menuName = "GAS/Effect Data")]
    public class EffectData : ScriptableObject
    {
        public string Name;
        public EffectType Type;
        public float Magnitude;
        public string[] Tags;
        public float Duration;
        public int Priority;
        public float ChainRange;
        public int MaxChainTargets;
        public float AreaRadius;
        public float DamageReduction;
    }

    public enum EffectType
    {
        Instant,
        Duration,
        Periodic,
        Chain,
        Area
    }
} 