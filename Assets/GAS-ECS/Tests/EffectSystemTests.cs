using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;
using GAS.Effects;

public class EffectSystemTests : ECSTestsFixture
{
    private EntityManager entityManager;
    private World world;
    private Entity targetEntity;
    private Entity sourceEntity;
    private EffectProcessingSystem effectProcessingSystem;
    private EffectPredictionSystem effectPredictionSystem;
    private EffectRollbackSystem effectRollbackSystem;
    private EffectVisualizationSystem effectVisualizationSystem;
    private EffectCombinationSystem effectCombinationSystem;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;

        // 创建目标实体
        targetEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(targetEntity, new HealthComponent { CurrentHealth = 100f, MaxHealth = 100f });
        entityManager.AddComponentData(targetEntity, new MovementComponent { Speed = 5f });
        entityManager.AddComponentData(targetEntity, new ShieldComponent { CurrentShield = 0f, MaxShield = 50f });
        entityManager.AddComponentData(targetEntity, new EnergyComponent { CurrentEnergy = 100f, MaxEnergy = 100f });
        entityManager.AddComponentData(targetEntity, new NetworkEntity { NetworkId = new NetworkEntityId { Value = 1 } });

        // 创建源实体
        sourceEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(sourceEntity, new NetworkEntity { NetworkId = new NetworkEntityId { Value = 2 } });

        // 获取系统
        effectProcessingSystem = world.GetOrCreateSystemManaged<EffectProcessingSystem>();
        effectPredictionSystem = world.GetOrCreateSystemManaged<EffectPredictionSystem>();
        effectRollbackSystem = world.GetOrCreateSystemManaged<EffectRollbackSystem>();
        effectVisualizationSystem = world.GetOrCreateSystemManaged<EffectVisualizationSystem>();
        effectCombinationSystem = world.GetOrCreateSystemManaged<EffectCombinationSystem>();
    }

    [Test]
    public void TestInstantDamageEffect()
    {
        // 创建即时伤害效果
        var effectEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Damage");
        entityManager.AddComponentData(effectEntity, new EffectComponent
        {
            Type = EffectType.Instant,
            Magnitude = 50f,
            Duration = 0f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity
        });

        // 更新系统
        effectProcessingSystem.Update();

        // 验证伤害效果
        var health = entityManager.GetComponentData<HealthComponent>(targetEntity);
        Assert.AreEqual(50f, health.CurrentHealth);
    }

    [Test]
    public void TestDurationHealEffect()
    {
        // 创建持续治疗效果
        var effectEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Heal");
        entityManager.AddComponentData(effectEntity, new EffectComponent
        {
            Type = EffectType.Duration,
            Magnitude = 10f,
            Duration = 5f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity
        });

        // 设置初始生命值
        var health = entityManager.GetComponentData<HealthComponent>(targetEntity);
        health.CurrentHealth = 50f;
        entityManager.SetComponentData(targetEntity, health);

        // 更新系统
        effectProcessingSystem.Update();

        // 验证治疗效果
        health = entityManager.GetComponentData<HealthComponent>(targetEntity);
        Assert.AreEqual(60f, health.CurrentHealth);
    }

    [Test]
    public void TestPeriodicSpeedEffect()
    {
        // 创建周期速度效果
        var effectEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Speed");
        entityManager.AddComponentData(effectEntity, new EffectComponent
        {
            Type = EffectType.Periodic,
            Magnitude = 1.5f,
            Duration = 10f,
            Period = 2f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity
        });

        // 更新系统
        effectProcessingSystem.Update();

        // 验证速度效果
        var movement = entityManager.GetComponentData<MovementComponent>(targetEntity);
        Assert.AreEqual(7.5f, movement.Speed);
    }

    [Test]
    public void TestChainEffect()
    {
        // 创建目标实体2
        var targetEntity2 = entityManager.CreateEntity();
        entityManager.AddComponentData(targetEntity2, new HealthComponent { CurrentHealth = 100f, MaxHealth = 100f });
        entityManager.AddComponentData(targetEntity2, new NetworkEntity { NetworkId = new NetworkEntityId { Value = 3 } });

        // 创建连锁伤害效果
        var effectEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Damage");
        entityManager.AddComponentData(effectEntity, new EffectComponent
        {
            Type = EffectType.Chain,
            Magnitude = 30f,
            Duration = 0f,
            ChainRange = 10f,
            MaxChainTargets = 2,
            DamageReduction = 0.5f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity
        });

        // 更新系统
        effectProcessingSystem.Update();

        // 验证连锁伤害效果
        var health1 = entityManager.GetComponentData<HealthComponent>(targetEntity);
        var health2 = entityManager.GetComponentData<HealthComponent>(targetEntity2);
        Assert.AreEqual(70f, health1.CurrentHealth);
        Assert.AreEqual(85f, health2.CurrentHealth);
    }

    [Test]
    public void TestAreaEffect()
    {
        // 创建目标实体2
        var targetEntity2 = entityManager.CreateEntity();
        entityManager.AddComponentData(targetEntity2, new HealthComponent { CurrentHealth = 100f, MaxHealth = 100f });
        entityManager.AddComponentData(targetEntity2, new NetworkEntity { NetworkId = new NetworkEntityId { Value = 3 } });

        // 创建范围伤害效果
        var effectEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Damage");
        entityManager.AddComponentData(effectEntity, new EffectComponent
        {
            Type = EffectType.Area,
            Magnitude = 40f,
            Duration = 0f,
            AreaRadius = 10f,
            DamageReduction = 0.5f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity
        });

        // 更新系统
        effectProcessingSystem.Update();

        // 验证范围伤害效果
        var health1 = entityManager.GetComponentData<HealthComponent>(targetEntity);
        var health2 = entityManager.GetComponentData<HealthComponent>(targetEntity2);
        Assert.AreEqual(60f, health1.CurrentHealth);
        Assert.AreEqual(80f, health2.CurrentHealth);
    }

    [Test]
    public void TestEffectPrediction()
    {
        // 创建预测效果
        var effectEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Damage");
        entityManager.AddComponentData(effectEntity, new PredictedEffectComponent
        {
            Type = EffectType.Instant,
            Magnitude = 50f,
            Duration = 0f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity,
            PredictionTime = 0.5f
        });

        // 更新预测系统
        effectPredictionSystem.Update();

        // 验证预测效果
        var health = entityManager.GetComponentData<HealthComponent>(targetEntity);
        Assert.AreEqual(50f, health.CurrentHealth);
    }

    [Test]
    public void TestEffectRollback()
    {
        // 创建服务器状态
        var serverStateEntity = entityManager.CreateEntity();
        var effectTags = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags[0] = new FixedString32("Damage");
        entityManager.AddComponentData(serverStateEntity, new ServerStateComponent
        {
            Type = EffectType.Instant,
            Magnitude = 30f,
            Duration = 0f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags)
        });
        entityManager.AddComponentData(serverStateEntity, new NetworkEntity { NetworkId = new NetworkEntityId { Value = 1 } });

        // 创建预测效果
        var predictedEffectEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(predictedEffectEntity, new PredictedEffectComponent
        {
            Type = EffectType.Instant,
            Magnitude = 50f,
            Duration = 0f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags),
            Source = sourceEntity,
            Target = targetEntity,
            PredictionTime = 0.5f
        });
        entityManager.AddComponentData(predictedEffectEntity, new NetworkEntity { NetworkId = new NetworkEntityId { Value = 1 } });

        // 更新回滚系统
        effectRollbackSystem.Update();

        // 验证回滚效果
        var health = entityManager.GetComponentData<HealthComponent>(targetEntity);
        Assert.AreEqual(70f, health.CurrentHealth);
    }

    [Test]
    public void TestEffectCombination()
    {
        // 创建两个伤害效果
        var effectEntity1 = entityManager.CreateEntity();
        var effectTags1 = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags1[0] = new FixedString32("Damage");
        entityManager.AddComponentData(effectEntity1, new EffectComponent
        {
            Type = EffectType.Instant,
            Magnitude = 30f,
            Duration = 0f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags1),
            Source = sourceEntity,
            Target = targetEntity,
            Priority = 1
        });

        var effectEntity2 = entityManager.CreateEntity();
        var effectTags2 = new NativeArray<FixedString32>(1, Allocator.Persistent);
        effectTags2[0] = new FixedString32("Damage");
        entityManager.AddComponentData(effectEntity2, new EffectComponent
        {
            Type = EffectType.Instant,
            Magnitude = 20f,
            Duration = 0f,
            Tags = new BlobAssetReference<EffectTagsBlob>(effectTags2),
            Source = sourceEntity,
            Target = targetEntity,
            Priority = 2
        });

        // 更新组合系统
        effectCombinationSystem.Update();

        // 验证组合效果
        var health = entityManager.GetComponentData<HealthComponent>(targetEntity);
        Assert.AreEqual(50f, health.CurrentHealth);
    }

    [TearDown]
    public override void TearDown()
    {
        base.TearDown();
    }
} 