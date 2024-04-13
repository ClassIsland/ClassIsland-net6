# 新增功能

1.3.1.0 ~ 1.3.3.0 (1.4-Firefly Beta 1~3) 的新增功能

> **您当前正在使用的是 1.4-Firefly 的测试版本，可能包含未完善和不稳定的功能。**

## 朗读提醒内容

在发出提醒时，ClassIsland可以大声读出提醒的内容。此功能默认禁用，您可以前往[【设置】->【提醒】->【更多选项】](ci://app/settings/notification)调整相关设置。

## 增强提醒

在发出提醒时，ClassIsland会全屏播放提醒特效，并且可以播放提示音效，增强提醒效果。提醒音效默认禁用，您可以自定义要播放的提示音效。您还可以给每个提醒来源单独设置音效。

您可以在[【设置】->【提醒】->【更多选项】](ci://app/settings/notification)调整相关设置。

![1712379341205](pack://application:,,,/ClassIsland;component/Assets/Documents/image/ChangeLog/1712379341205.png)

## 精确时间

ClassIsland现在支持从服务器同步当前的精确时间。也可以在此基础上自定义时间偏移值，用于微调时间。此外，ClassIsland还支持在每天以设定的增量值调整时间偏移值。您可以前往[【设置】->【基本】->【时钟】](ci://app/settings/general)调整相关设置。

![1711863876445](pack://application:,,,/ClassIsland;component/Assets/Documents/image/ChangeLog/1711863876445.png)

## 集控

您可以将ClassIsland实例加入到集控中，以统一分发课表、时间表等信息，并控制各实例的行为。

[了解更多…](https://github.com/HelloWRC/ClassIsland/wiki/%E9%9B%86%E6%8E%A7)

[#43](https://github.com/HelloWRC/ClassIsland/issues/43)

![1711241863976](pack://application:,,,/ClassIsland;component/Assets/Documents/image/ChangeLog/1711241863976.png)

![1711241942861](pack://application:,,,/ClassIsland;component/Assets/Documents/image/ChangeLog/1711241942861.png)



***


# 1.3.3.0

> 1.4 - Firefly 测试版，可能包含未完善和不稳定的功能。

## 新增功能

- 【提醒】全屏提醒强调特效 
- 【提醒】提醒强调音效 [#41](https://github.com/HelloWRC/ClassIsland/issues/41)
- 【提醒】按提醒来源分别设置语音、强调特效和音效开关 
- 【提醒】允许禁用准备上课提醒文字 [#64](https://github.com/HelloWRC/ClassIsland/issues/64)
- 【时钟】设置时间偏移值 [#55](https://github.com/HelloWRC/ClassIsland/issues/55)
- 【时钟】按增量自动调整时间偏移值 [#58](https://github.com/HelloWRC/ClassIsland/issues/58)

## 优化与Bug修复
- 【档案编辑】修复在编辑科目时，在编辑状态下添加科目导致科目信息丢失的问题 [#62](https://github.com/HelloWRC/ClassIsland/issues/62)
- 【提醒】修复了即将上课时，当倒计时为0时数字不显示的问题。
- 【UI】优化上下课提醒设置界面。
- 【UI】优化时钟设置界面。

***


# 1.3.2.0

> 1.4 - Firefly 测试版，可能包含未完善和不稳定的功能。

## 新增功能
- 【应用】从NTP服务器获取当前时间
- 【提醒】支持使用EdgeTTS朗读服务

## 优化与Bug修复
- 【提醒】在播放大于等于一小时的时间时不发出语音（[#51](https://github.com/HelloWRC/ClassIsland/issues/51)）



***


# 1.3.1.0

> 1.4 - Firefly 测试版，可能包含未完善和不稳定的功能。

## 新增功能
- 【集控】手动加入集控
- 【集控】拉取与合并档案信息
- 【集控】加载功能限制策略
- 【提醒】朗读提醒内容
- 【帮助文档】加入新增共能内容

## 优化与Bug修复
- 【UI】修复加载动画中版本号被进度条遮挡的问题