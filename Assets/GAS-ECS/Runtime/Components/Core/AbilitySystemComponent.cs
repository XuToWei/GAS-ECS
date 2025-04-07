using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;

namespace GAS.Core
{
    public struct AbilitySystemComponent : IComponentData
    {
        public Entity Owner;
        public BlobAssetReference<AbilitySetBlob> AbilitySet;
        public NativeHashMap<FixedString32, float> Attributes;
        public NativeList<FixedString32> Tags;
        public NativeList<ActiveAbility> ActiveAbilities;
        public NativeList<ActiveEffect> ActiveEffects;
        public int PredictionKey;
        public int ServerPredictionKey;

        public void Initialize()
        {
            Attributes = new NativeHashMap<FixedString32, float>(32, Allocator.Persistent);
            Tags = new NativeList<FixedString32>(16, Allocator.Persistent);
            ActiveAbilities = new NativeList<ActiveAbility>(8, Allocator.Persistent);
            ActiveEffects = new NativeList<ActiveEffect>(16, Allocator.Persistent);
            PredictionKey = 0;
            ServerPredictionKey = 0;
        }

        public void Dispose()
        {
            if (Attributes.IsCreated)
                Attributes.Dispose();
            if (Tags.IsCreated)
                Tags.Dispose();
            if (ActiveAbilities.IsCreated)
                ActiveAbilities.Dispose();
            if (ActiveEffects.IsCreated)
                ActiveEffects.Dispose();
        }

        public void SetAttributeValue(FixedString32 attributeName, float value)
        {
            if (!Attributes.ContainsKey(attributeName))
                Attributes.Add(attributeName, value);
            else
                Attributes[attributeName] = value;
        }

        public float GetAttributeValue(FixedString32 attributeName)
        {
            return Attributes.TryGetValue(attributeName, out float value) ? value : 0f;
        }

        public void AddTag(FixedString32 tag)
        {
            if (!Tags.Contains(tag))
                Tags.Add(tag);
        }

        public void RemoveTag(FixedString32 tag)
        {
            for (int i = 0; i < Tags.Length; i++)
            {
                if (Tags[i].Equals(tag))
                {
                    Tags.RemoveAtSwapBack(i);
                    break;
                }
            }
        }

        public bool HasTag(FixedString32 tag)
        {
            return Tags.Contains(tag);
        }

        public void AddActiveAbility(ActiveAbility ability)
        {
            ActiveAbilities.Add(ability);
        }

        public void RemoveActiveAbility(int index)
        {
            if (index >= 0 && index < ActiveAbilities.Length)
                ActiveAbilities.RemoveAtSwapBack(index);
        }

        public void AddActiveEffect(ActiveEffect effect)
        {
            ActiveEffects.Add(effect);
        }

        public void RemoveActiveEffect(int index)
        {
            if (index >= 0 && index < ActiveEffects.Length)
                ActiveEffects.RemoveAtSwapBack(index);
        }

        public int GeneratePredictionKey()
        {
            return ++PredictionKey;
        }

        public void SetServerPredictionKey(int key)
        {
            ServerPredictionKey = key;
        }
    }

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
    }

    public struct EffectData
    {
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobArray<FixedString32> Tags;
    }

    public enum EffectType
    {
        Instant,
        Duration,
        Periodic
    }

    public struct ActiveAbility
    {
        public FixedString32 Name;
        public float Cooldown;
        public float Cost;
        public int PredictionKey;
        public bool IsActive;
    }

    public struct ActiveEffect
    {
        public FixedString32 Name;
        public float Duration;
        public float Magnitude;
        public int PredictionKey;
        public bool IsActive;
    }
} 