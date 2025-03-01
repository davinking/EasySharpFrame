namespace ES.Common.Time
{
    /// <summary>
    /// 时间流 更新接口
    /// <para>[多线程处理逻辑] Update以10ms周期循环</para>
    /// <para>继承此类可以实现Update实时更新功能</para>
    /// <para>为了方便类的部分初始和性能节省需手动调用 StartTimeFlow(); 函数</para>
    /// <para>每次Update是先执行函数体内容再睡眠等待，所以如果需要精确的时间间隔应当先判定时间再累加时间</para>
    /// <para>继承此类的对象会分配在多个线程下运行，需要单线程请使用SyncTimeFlow类</para>
    /// </summary>
    public interface ITimeUpdate
    {
        /// <summary>
        /// 更新 Update以10ms周期循环 
        /// <para>可以通过此timeFlowPeriod对象直接获取</para>
        /// <para>每次Update是先执行函数体内容再睡眠等待，所以如果需要精确的时间间隔应当先判定时间再累加时间</para>
        /// </summary>
        /// <param name="deltaTime">程序函数实际执行时间间隔 精度：ms</param>
        void Update(int deltaTime);

        /// <summary>
        /// 停止更新
        /// <para>关闭时间流触发此函数</para>
        /// </summary>
        void UpdateEnd();
    }
}
