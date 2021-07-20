﻿namespace ES.Hotfix
{
    /// <summary>
    /// 热更代理
    /// <para>只有继承这个代理类才能在热更层使用其泛型的变量值</para>
    /// <para>继承此类使用数据层变量请用 self 代替 this </para>
    /// <para>如果需要在代理构造内使用代理数据只需要创建仅有一个构造参数的构造函数，构造参数为当前代理数据对象类型即可使用</para>
    /// <para>代理类中函数如果存在委托情况，请注意如仅存于非热更层的委托可能存在问题</para>
    /// <para>此类事代理类，请不要使用代理类来直接声明变量或属性</para>
    /// <para>当然如果真的有需要，可以使用 AgentDataPivot 类来存储变量实现热更层声明变量</para>
    /// <para>热更层可以继承时间流接口，每次热更重载后时间流重置</para>
    /// <para>当存在对应关系的代理 AgentData 继承对象时，会在构建代理数据 AgentData 时自动构建此类继承的子类</para>
    /// <para>当然如果不要自动构建可以在 AgentData 继承类添加 [NotCreateAgent] 特性, 即可手动通过 GetAgent 函数创建</para>
    /// <para>如果确实需要在热更层新建字段或属性，并且希望可以重载之后值也可以保留，那么可以使用 [CopyAgentValue] 特性</para>
    /// </summary>
    /// <typeparam name="T">代理数据类型</typeparam>
    public abstract class Agent<T> : BaseAgent where T : AgentData
    {
        /// <summary>
        /// 代理类对象
        /// <para>通过此对象可以获取代理数据的对象，相当于this的用法</para>
        /// </summary>
#pragma warning disable IDE1006 // 命名样式
        protected T self => _self;
#pragma warning restore IDE1006 // 命名样式
    }
}
