﻿namespace ES.Network.Sockets
{
    /// <summary>
    /// 远程套接字委托接口回调
    /// </summary>
    public interface RemoteSocketInvoke
    {
        /// <summary>
        /// 完成接受回调
        /// </summary>
        /// <param name="msg">数据信息体</param>
        void ReceivedCompleted(RemoteSocketMsg msg);
    }
}
