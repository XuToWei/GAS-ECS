using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace GAS.Core
{
    public struct AbilitySetBlob
    {
        public BlobArray<AbilityData> Abilities;
    }

    public struct AbilityData
    {
        public FixedString32 Name;
        public float Cooldown;
        public float Cost;
        public BlobArray<EffectData> Effects;
        public BlobArray<AbilityCondition> Conditions;
        public BlobArray<AbilityTrigger> Triggers;
    }

    public struct EffectData
    {
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobArray<FixedString32> Tags;
        public BlobArray<byte> CustomData;
    }

    public enum EffectType
    {
        Instant,
        Duration,
        Periodic,
        Chain,
        Area,
        Custom
    }
} 