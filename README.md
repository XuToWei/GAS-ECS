# Unity DOTS Gameplay Ability System (GAS)

这是一个基于Unity DOTS实现的游戏能力系统，参考了Unreal Engine的GAS系统。该系统提供了完整的游戏能力、效果和属性系统，支持网络同步和客户端预测。

## 功能特性

- 基于DOTS的高性能实现
- 支持即时、持续时间和周期性效果
- 完整的网络同步系统
- 客户端预测支持
- 可视化编辑器工具
- 基于标签的属性系统

## 系统要求

- Unity 2022.3 或更高版本
- DOTS 包
- NetCode 包

## 安装

1. 克隆此仓库到你的Unity项目中
2. 确保已安装所需的包
3. 打开Window > Package Manager，安装以下包：
   - Entities
   - NetCode
   - Burst
   - Mathematics
   - Collections

## 使用方法

### 创建能力

1. 打开Window > GAS > Ability Editor
2. 填写能力信息：
   - 能力名称
   - 冷却时间
   - 消耗值
3. 添加效果：
   - 选择效果类型（即时/持续时间/周期性）
   - 设置效果数值
   - 添加标签
4. 点击"Create Ability"创建能力

### 在实体上使用能力系统

```csharp
// 创建能力系统组件
var abilitySystem = new AbilitySystemComponent
{
    Owner = entity,
    AbilitySet = abilitySetBlob,
    ActiveAbilities = new NativeList<Entity>(Allocator.Persistent),
    ActiveEffects = new NativeList<Entity>(Allocator.Persistent),
    Attributes = new NativeHashMap<FixedString32, float>(16, Allocator.Persistent),
    IsServer = isServer
};

// 添加组件到实体
EntityManager.AddComponentData(entity, abilitySystem);
```

### 激活能力

```csharp
// 检查能力是否可用
if (CanActivateAbility(abilityName))
{
    // 激活能力
    ActivateAbility(abilityName);
}
```

## 网络同步

系统使用预测键（Prediction Key）来处理客户端预测：

1. 客户端发送能力激活请求
2. 客户端立即预测效果
3. 服务器验证请求
4. 如果验证失败，客户端回滚预测的效果

## 性能优化

- 使用BlobAsset存储能力数据
- 使用NativeCollections进行内存管理
- 支持Burst编译
- 基于DOTS的并行处理

## 贡献

欢迎提交Issue和Pull Request！

## 许可证

MIT License