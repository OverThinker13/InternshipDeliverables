# 实现逻辑与代码审计指南

## 目录

- 核心文件
- 总体数据流
- 上下文构建
- 曝光链路
- 参与链路
- 充值链路
- 去重与幂等
- 异常处理
- 审计方法
- 测试用例
- 可演进方向

## 核心文件

- `client/Assets/Scripts/Hotfix/Core/WebService/ActivityTrack.cs`：事件枚举、请求 DTO、响应 DTO。
- `client/Assets/Scripts/Hotfix/Core/WebService/Manager.cs`：三个后端接口的路由和请求处理。
- `client/Assets/Scripts/Hotfix/Features/Activities/ActivityTrackManager.cs`：统一埋点管理器。
- `client/Assets/Scripts/Hotfix/Features/Recharge.cs`：活动充值上下文透传、订单登记和到账确认。
- `client/Assets/Scripts/Hotfix/UI/Controllers/ResidentActivities/ResidentActivities.cs`：统一活动容器曝光。
- `client/Assets/Scripts/Hotfix/UI/Controllers/SpecialActivities/WeeksCardActivity.cs`：独立窗口曝光示例。
- `client/Assets/Scripts/Hotfix/Features/Activities/Deprecated/SpecialActivities/WeeksCard.cs`：遗留活动参与示例。
- `client/Assets/Scripts/Hotfix/Features/Activities/Services/**`：各活动的参与和充值接点。
- `xlsx/Datas/HD活动/HD活动控制表.xlsx`：活动主配置。
- `xlsx/Datas/table.xml`：Luban 表映射。
- `client/Assets/Scripts/Hotfix/Features/Activities/EPromotion.cs`：客户端活动枚举。

## 总体数据流

```text
活动 UI / Service
    ↓ 传入 Promotion 或 ActivityTrackContext
ActivityTrackManager
    ↓ 校验 + 去重 + 持久化
WebServerManager.ReportActivityTrack
    ↓
ActivityTrack/reportExposure
ActivityTrack/reportParticipation
ActivityTrack/reportRecharge
```

充值额外经过：

```text
活动购买按钮
    ↓
RechargeOrder(productId, promotion/context)
    ↓ 游戏服创建订单
RegisterRechargeOrder(gameOrderId, productId, context)
    ↓ SDK 支付 + 服务器发货
RechargeRewardMessage
    ↓
ConfirmRecharge(productId)
    ↓
ReportRecharge(gameOrderId, context)
```

## 上下文构建

`ActivityTrackContext` 是业务层与埋点层之间的最小契约。优先从当前 `Promotion` 构建，因为它同时提供：

- `Promotion.Id`：活动归因 ID。
- `Promotion.EType`：判断是否运营类活动。
- `Promotion.Duty.AliveTime / ExpiredTime`：本期活动时间。

独立活动没有完整 `Promotion` 时，使用 `Create(EPromotion, isOperate, start, end)` 显式构建。不要把 UI 名称、页签序号或商品 ID 当作 `promotion_id`。

## 曝光链路

曝光应在 UI 真正显示之后触发，而不是点击入口之前。检查时回答三个问题：

1. 玩家是否已经看到了活动内容？
2. 当前窗口能否唯一确定活动 ID？
3. 重复切换或重新打开是否由每日缓存去重？

自动曝光通过扫描 `PromotionViewAttribute` 构造 `Type -> EPromotion` 映射。一个 Type 对应多个 ID 时不加入自动映射，防止错误归因。统一活动容器使用当前选中的 `data.promotion` 显式上报。

## 参与链路

参与点必须放在业务成功后。推荐结构：

```csharp
var response = await CallRequest<SomeResponse>(request);
if (response == null) return false;

UpdateModule(response);
_ = Manager.Instance.Get<ActivityTrackManager>().ReportParticipation(owner);
return true;
```

不要在以下位置上报：

- 仅点击按钮时。
- 参数校验之前。
- 网络请求发出之前。
- 服务器可能拒绝但尚未确认时。

同一个活动可能有多个“有效参与”操作。由于管理器按日去重，多个成功操作最终只记录一次参与，但每个入口仍应接入，确保玩家走任一路径都能被捕获。

## 充值链路

活动充值必须使用携带上下文的重载：

```csharp
Manager.Instance.Get<Recharge>().RechargeOrder(productId, owner);
```

或显式传 `ActivityTrackContext`。普通 `RechargeOrder(productId)` 无法知道充值来自哪个活动，因此不会产生活动充值归因。

订单创建成功后保存上下文的原因：SDK 回调和发货消息发生得更晚，原 UI 或 `Promotion` 可能已经关闭或销毁。保存纯数据而非对象引用可以跨场景、跨登录恢复。

充值上报不能使用每日去重，因为同一玩家同一天可以在同一活动产生多笔真实订单。充值幂等依赖 `game_order_id` 和后端处理；客户端仅防止同一待处理订单同时重复发送。

## 去重与幂等

曝光和参与缓存 Key：

```text
ActivityTrack_{TrackType}_{RoleId}_{ServerId}_{PromotionId}
```

缓存值是服务器时间格式化后的 `yyyyMMdd`。只有后端响应满足：

- `code == 200`
- `isRecorded == 1`

才写入每日缓存。`isRecorded == 0` 不是请求错误，但客户端不缓存，下次触发仍会请求。这是后端契约决定的行为，评审时需要确认是否符合预期。

`reportingKeys` 防止同一天同一事件在上一次请求未结束时再次发送。充值用 `reportingRechargeOrders` 按订单防并发。

## 异常处理

- 请求对象为空或上下文无效时直接忽略并记录警告。
- 网络、数据格式和后端响应为空时不写缓存。
- 异步异常统一记录日志，避免影响活动主流程。
- 埋点调用多为 fire-and-forget，业务成功不依赖统计接口成功。
- 已支付订单上报失败时继续保存在 Storage，下一次登录补报。

这种设计保证统计系统故障不会阻塞玩家领取奖励或充值发货，同时尽量保留数据完整性。

## 审计方法

使用以下搜索快速恢复全貌：

```powershell
rg -n "ActivityTrack" client/Assets/Scripts/Hotfix -g "*.cs"
rg -n "ReportParticipation" client/Assets/Scripts/Hotfix/Features/Activities -g "*.cs"
rg -n "RechargeOrder\(" client/Assets/Scripts/Hotfix/Features/Activities -g "*.cs"
rg -n "PromotionView" client/Assets/Scripts/Hotfix -g "*.cs"
```

再逐个检查：

- 曝光是否发生在 `ShowView` 之后。
- 参与是否发生在成功响应之后。
- 活动现金购买是否传入 `owner`。
- 特殊活动是否手工构建正确上下文。
- 活动时间单位是否从毫秒转成秒。
- 缓存 Key 是否包含角色和区服。
- Excel、枚举和 Service Attribute 是否同步。

## 测试用例

### 曝光

- 第一次打开活动，产生一次请求。
- 同一天重复打开，不重复记录。
- 次日打开，再次记录。
- 切换角色后打开，同一设备不应被前一角色缓存挡住。
- 多 ID 共用窗口时，不应自动错报。

### 参与

- 点击后请求失败，不上报参与。
- 成功领取或完成操作，上报一次。
- 同一天从另一参与入口成功，不重复计数。
- 后端返回 `isRecorded = 0` 时，下次触发仍会请求。

### 充值

- 创建订单后取消支付，不上报充值。
- 支付成功且收到发货，上报对应游戏订单号。
- 埋点接口失败，待处理记录保留。
- 重新登录后补报成功并清理记录。
- 同一商品连续创建两笔订单，验证当前按商品 ID 确认是否会错配。
- 编辑器 GM 支付路径也能登记并确认。

## 可演进方向

- 让支付成功消息携带 `game_order_id`，按订单精确确认。
- 为待处理订单增加创建时间、最大重试次数或过期策略。
- 将 `is_operate` 收敛为唯一配置来源。
- 自动生成或校验 `EPromotion`，消除 Excel 与枚举漂移。
- 建立活动 Service 的统一参与事件接口，减少 100 多处手工接点。
- 增加开发期审计脚本，输出未接曝光、参与或充值上下文的活动清单。
- 建立后端对账：游戏支付订单数、活动充值埋点数、BI 入库数三方核对。


