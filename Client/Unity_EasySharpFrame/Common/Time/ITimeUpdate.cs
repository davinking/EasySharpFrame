namespace ES.Common.Time
{
    /// <summary>
    /// ʱ���� ���½ӿ�
    /// <para>[���̴߳����߼�] Update��10ms����ѭ��</para>
    /// <para>�̳д������ʵ��Updateʵʱ���¹���</para>
    /// <para>Ϊ�˷�����Ĳ��ֳ�ʼ�����ܽ�ʡ���ֶ����� StartTimeFlow(); ����</para>
    /// <para>ÿ��Update����ִ�к�����������˯�ߵȴ������������Ҫ��ȷ��ʱ����Ӧ�����ж�ʱ�����ۼ�ʱ��</para>
    /// <para>�̳д���Ķ��������ڶ���߳������У���Ҫ���߳���ʹ��SyncTimeFlow��</para>
    /// </summary>
    public interface ITimeUpdate
    {
        /// <summary>
        /// ���� Update��10ms����ѭ�� 
        /// <para>����ͨ����timeFlowPeriod����ֱ�ӻ�ȡ</para>
        /// <para>ÿ��Update����ִ�к�����������˯�ߵȴ������������Ҫ��ȷ��ʱ����Ӧ�����ж�ʱ�����ۼ�ʱ��</para>
        /// </summary>
        /// <param name="deltaTime">������ʵ��ִ��ʱ���� ���ȣ�ms</param>
        void Update(int deltaTime);

        /// <summary>
        /// ֹͣ����
        /// <para>�ر�ʱ���������˺���</para>
        /// </summary>
        void UpdateEnd();
    }
}
