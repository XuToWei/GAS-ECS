using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using GAS.Core;
using GAS.Abilities;

public class TriggerEffectExample : MonoBehaviour
{
    public TriggerEffectAsset damageTriggerEffect;
    public TriggerEffectAsset healTriggerEffect;
    public TriggerEffectAsset killTriggerEffect;
    public TriggerEffectAsset customTriggerEffect;

    private EntityManager entityManager;
    private Entity playerEntity;

    private void Start()
    {
        // 获取EntityManager
        var world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;

        // 创建玩家实体
        CreatePlayerEntity();
    }

    private void CreatePlayerEntity()
    {
        // 创建实体
        playerEntity = entityManager.CreateEntity();

        // 添加能力系统组件
        var abilitySystem = new AbilitySystemComponent();
        abilitySystem.Initialize();

        // 设置基本属性
        abilitySystem.SetAttributeValue(new FixedString32("Health"), 100f);
        abilitySystem.SetAttributeValue(new FixedString32("MaxHealth"), 100f);
        abilitySystem.SetAttributeValue(new FixedString32("Speed"), 5f);
        abilitySystem.SetAttributeValue(new FixedString32("Shield"), 0f);
        abilitySystem.SetAttributeValue(new FixedString32("Energy"), 100f);
        abilitySystem.SetAttributeValue(new FixedString32("MaxEnergy"), 100f);

        // 添加组件
        entityManager.AddComponentData(playerEntity, abilitySystem);
    }

    private void Update()
    {
        if (!entityManager.Exists(playerEntity))
            return;

        // 测试触发器效果
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ApplyDamageTriggerEffect();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ApplyHealTriggerEffect();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ApplyKillTriggerEffect();
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplyCustomTriggerEffect();
        }
    }

    private void ApplyDamageTriggerEffect()
    {
        if (damageTriggerEffect == null || !damageTriggerEffect.TriggerEffect.IsCreated)
            return;

        // 创建触发器效果实体
        var triggerEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(triggerEntity, new TriggerEffectComponent
        {
            Owner = playerEntity,
            TriggerType = TriggerType.OnDamage,
            Cooldown = damageTriggerEffect.TriggerEffect.Value.Cooldown,
            Tags = damageTriggerEffect.TriggerEffect.Value.RequiredTags,
            CustomData = damageTriggerEffect.TriggerEffect.Value.CustomData
        });
    }

    private void ApplyHealTriggerEffect()
    {
        if (healTriggerEffect == null || !healTriggerEffect.TriggerEffect.IsCreated)
            return;

        var triggerEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(triggerEntity, new TriggerEffectComponent
        {
            Owner = playerEntity,
            TriggerType = TriggerType.OnHeal,
            Cooldown = healTriggerEffect.TriggerEffect.Value.Cooldown,
            Tags = healTriggerEffect.TriggerEffect.Value.RequiredTags,
            CustomData = healTriggerEffect.TriggerEffect.Value.CustomData
        });
    }

    private void ApplyKillTriggerEffect()
    {
        if (killTriggerEffect == null || !killTriggerEffect.TriggerEffect.IsCreated)
            return;

        var triggerEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(triggerEntity, new TriggerEffectComponent
        {
            Owner = playerEntity,
            TriggerType = TriggerType.OnKill,
            Cooldown = killTriggerEffect.TriggerEffect.Value.Cooldown,
            Tags = killTriggerEffect.TriggerEffect.Value.RequiredTags,
            CustomData = killTriggerEffect.TriggerEffect.Value.CustomData
        });
    }

    private void ApplyCustomTriggerEffect()
    {
        if (customTriggerEffect == null || !customTriggerEffect.TriggerEffect.IsCreated)
            return;

        var triggerEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(triggerEntity, new TriggerEffectComponent
        {
            Owner = playerEntity,
            TriggerType = TriggerType.Custom,
            Cooldown = customTriggerEffect.TriggerEffect.Value.Cooldown,
            Tags = customTriggerEffect.TriggerEffect.Value.RequiredTags,
            CustomData = customTriggerEffect.TriggerEffect.Value.CustomData
        });
    }

    private void OnGUI()
    {
        GUILayout.Label("Trigger Effect Example", EditorStyles.boldLabel);
        GUILayout.Label("Press 1: Apply Damage Trigger Effect");
        GUILayout.Label("Press 2: Apply Heal Trigger Effect");
        GUILayout.Label("Press 3: Apply Kill Trigger Effect");
        GUILayout.Label("Press 4: Apply Custom Trigger Effect");

        if (entityManager.Exists(playerEntity))
        {
            var abilitySystem = entityManager.GetComponentData<AbilitySystemComponent>(playerEntity);
            GUILayout.Label($"Health: {abilitySystem.GetAttributeValue(new FixedString32("Health"))}");
            GUILayout.Label($"Shield: {abilitySystem.GetAttributeValue(new FixedString32("Shield"))}");
            GUILayout.Label($"Energy: {abilitySystem.GetAttributeValue(new FixedString32("Energy"))}");
        }
    }
} 