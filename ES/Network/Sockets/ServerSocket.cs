﻿using ES.Common.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ES.Network.Sockets
{
    using System.Net.Sockets;
    /// <summary>
    /// ESF套接字服务端管理
    /// 通过新建ESFSocket对象 装载到此类中即可生成一个服务器对象
    /// UDP访问仅仅通过本类不能监控其断开状态，需要自主定制
    /// </summary>
    public class ServerSocket
    {
        /// <summary>
        /// 客户端连接合集 tcp
        /// </summary>
        private List<RemoteConnection> remoteTCPClients = new List<RemoteConnection>();

        /// <summary>
        /// 客户端连接设计为同时处理的最大连接数
        /// the maximum number of connections the sample is designed to handle simultaneously 
        /// </summary>
        private int numConnections;
        /// <summary>
        /// 用于每个套接字I/O操作的缓冲区大小
        /// buffer size to use for each socket I/O operation 
        /// </summary>
        private int receiveBufferSize;
        /// <summary>
        /// 表示所有套接字操作的一组可重用的大缓冲区
        /// represents a large reusable set of buffers for all socket operations
        /// </summary>
        private BufferManager bufferManager;
        /// <summary>
        /// 读、写（不为接受分配缓冲区空间）
        /// read, write (don't alloc buffer space for accepts)
        /// </summary>
        private const int opsToPreAlloc = 2;
        /// <summary>
        /// 用于侦听传入连接请求的ESF套接字
        /// the socket used to listen for incoming connection requests
        /// </summary>
        private Socket serverSocket = null;
        /// <summary>
        /// 用于写入、读取和接受套接字操作的可重用SocketAsyncEventArgs对象池
        /// pool of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
        /// </summary>
        private ConcurrentStack<SocketAsyncEventArgs> readWritePool;
        /// <summary>
        /// 服务器接收的总字节数计数器
        /// counter of the total # bytes received by the server
        /// </summary>
        /// private int totalBytesRead;
        /// <summary>
        /// 连接到服务器的客户端总数
        /// the total number of clients connected to the server
        /// </summary>
        private int numConnectedSockets;
        /// <summary>
        /// 最大容许客户端数量
        /// </summary>
        private Semaphore maxNumberAcceptedClients;
        /// <summary>
        /// 消息委托
        /// </summary>
        public RemoteSocketInvoke socketInvoke = null;
        /// <summary>
        /// 套接字状态监听回调器
        /// </summary>
        internal SocketStatusListener socketStatusListener = null;
        /// <summary>
        /// 监听套接字状态任务
        /// </summary>
        private MonitorSocketStatusTask monitorSocketStatusTask = null;

        /// <summary>
        /// 新建套接字服务管理对象
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="numConnections">可同时连接客户端数</param>
        /// <param name="receiveBufferSize">接受数据大小[UDP模式以传送理论最大值/TCP模式以传送最佳合适值]</param>
        public ServerSocket(string ip, int port, int numConnections, int receiveBufferSize)
        {
            // 绑定原生套接字
            serverSocket = new Socket(ip, port);

            // 初始化部分参数
            // totalBytesRead = 0;
            numConnectedSockets = 0;
            this.numConnections = numConnections;
            this.receiveBufferSize = receiveBufferSize + SweetStream.OUTSOURCING_SIZE;

            // allocate buffers such that the maximum number of sockets can have one outstanding read and 
            // write posted to the socket simultaneously  
            bufferManager = new BufferManager(this.receiveBufferSize * numConnections * opsToPreAlloc,
                this.receiveBufferSize);
            readWritePool = new ConcurrentStack<SocketAsyncEventArgs>();
            maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
        }

        /// <summary>
        /// 新建套接字服务管理对象
        /// </summary>
        /// <param name="esfSocket">ESF套接字</param>
        /// <param name="numConnections">可同时连接客户端数</param>
        /// <param name="receiveBufferSize">接受数据大小[UDP模式以传送理论最大值/TCP模式以传送最佳合适值]</param>
        public ServerSocket(Socket esfSocket, int numConnections, int receiveBufferSize)
        {
            // 绑定原生套接字
            serverSocket = esfSocket;

            // 初始化部分参数
            // totalBytesRead = 0;
            numConnectedSockets = 0;
            this.numConnections = numConnections;
            this.receiveBufferSize = receiveBufferSize + SweetStream.OUTSOURCING_SIZE;

            // allocate buffers such that the maximum number of sockets can have one outstanding read and 
            // write posted to the socket simultaneously  
            bufferManager = new BufferManager(this.receiveBufferSize * numConnections * opsToPreAlloc,
                this.receiveBufferSize);
            readWritePool = new ConcurrentStack<SocketAsyncEventArgs>();
            maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
        }

        /// <summary>
        /// 初始化套接字服务管理
        /// </summary>
        /// <param name="addressFamily">地址协议簇 [ipv4或ipv6]</param>
        /// <param name="socketType">套接字类型 [stream或Dgram]</param>
        /// <param name="protocolType">协议类型 [tcp或者udp]</param>
        /// <param name="backlog">同时监听接入连接数 默认为0, 即 并发可监听数</param>
        /// <param name="socketInvoke">委托回调</param>
        /// <returns>连接是否成功</returns>
        public bool Init(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, int backlog, RemoteSocketInvoke socketInvoke)
        {
            // 绑定委托
            this.socketInvoke = socketInvoke;
            // Allocates one large byte buffer which all I/O operations use a piece of.  This gaurds 
            // against memory fragmentation
            bufferManager.InitBuffer();
            for (int i = 0; i < numConnections; i++)
            {
                // preallocate pool of SocketAsyncEventArgs objects 
                // Pre -allocate a set of reusable SocketAsyncEventArgs
                SocketAsyncEventArgs readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                RemoteConnection remoteClient = new RemoteConnection();
                // 如果是报文结构则 初始化终端节点
                if (socketType == SocketType.Dgram) readWriteEventArg.RemoteEndPoint = serverSocket.endPoint;
                readWriteEventArg.UserToken = remoteClient;
                remoteClient.readWriteEventArg = readWriteEventArg;
                // assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
                bufferManager.SetBuffer(readWriteEventArg);
                // add SocketAsyncEventArg to the pool
                readWritePool.Push(readWriteEventArg);
            }
            // 启动服务
            bool bSuccess = serverSocket.ConnectAsServer(addressFamily, socketType, protocolType, backlog);
            StartServer();
            // Log.Info("Init Sever Connecting!");
            return bSuccess;
        }

        /// <summary>
        /// 启动服务
        /// 现在不需要手动处理了，Init里直接执行
        /// </summary>
        /// <returns></returns>
        private void StartServer()
        {
            if (serverSocket.socketType == SocketType.Stream)
                StartAccept(null);
            if (serverSocket.socketType == SocketType.Dgram)
                StartAcceptReceiveFrom();
        }

        /// <summary>
        /// 返回客户端连接数
        /// </summary>
        /// <returns></returns>
        public int GetClientCount()
        {
            if (serverSocket.socketType == SocketType.Stream)
                lock (remoteTCPClients)
                    return remoteTCPClients.Count;
            // else if (serverSocket.socketType == SocketType.Dgram)
            //    return remoteUDPClients.Count;
            // else
            return -1;
        }

        /// <summary>
        /// 查询是否还存在指定客户端
        /// </summary>
        /// <param name="client"></param>
        public bool CheckExistClient(RemoteConnection client)
        {
            if (serverSocket.socketType == SocketType.Stream)
                lock (remoteTCPClients)
                    return remoteTCPClients.Contains(client);
            // else if (serverSocket.socketType == SocketType.Dgram)
            //     return remoteUDPClients.Values.Contains(client);
            return false;
        }

        /// <summary>
        /// 移除指定客户端
        /// </summary>
        /// <param name="client">指定客户端对象</param>
        internal void RemoveExistClient(RemoteConnection client)
        {
            if (serverSocket.socketType == SocketType.Stream)
            {
                lock (remoteTCPClients)
                {
                    if (remoteTCPClients.Contains(client))
                    {
                        remoteTCPClients.Remove(client);
                        CloseClientSocket(client.readWriteEventArg);
                    }
                }
            }
        }

        /// <summary>
        /// 设置套接字事件监听
        /// 单独设置监听 有可能无法检测到断开连接情况
        /// TCP可以使用监控任务 UDP无法通过监控判断其断开情况
        /// </summary>
        /// <param name="listener"></param>
        public void SetSocketEvent(SocketStatusListener listener)
        {
            SetSocketEvent(listener, new MonitorSocketStatusTask(-1));
        }

        /// <summary>
        /// 设置套接字事件监听且可定义监控任务
        /// UDP无需使用方法，因为无效
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="task"></param>
        public void SetSocketEvent(SocketStatusListener listener, MonitorSocketStatusTask task)
        {
            socketStatusListener = listener;
            monitorSocketStatusTask = task;
        }

        /// <summary>
        /// 开始接受新客户端连接
        /// IOCP模型构建
        /// </summary>
        /// <param name="acceptEventArg">异步事件参数</param>
        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            }
            else
            {
                // socket must be cleared since the context object is being reused
                acceptEventArg.AcceptSocket = null;
            }
            maxNumberAcceptedClients.WaitOne();
            bool willRaiseEvent = serverSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        /// <summary>
        /// 接受客户端连接事件完成端口回调
        /// </summary>
        private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                ProcessAccept(e);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "ServerConnection", "AcceptEventArg_Completed", "Socket");
            }
        }

        /// <summary>
        /// 处理新客户端连接
        /// </summary>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref numConnectedSockets);
            // log.Info("ProcessAccept", "Client connection accepted. There are {0} clients connected to the server", numConnectedSockets);

            // Get the socket for the accepted client connection and put it into the 
            //ReadEventArg object user token
            if (readWritePool.TryPop(out SocketAsyncEventArgs readEventArgs))
            {
                RemoteConnection client = (RemoteConnection)readEventArgs.UserToken;
                // 初始化
                client.Init(Socket.FillAsClient(e.AcceptSocket), this, socketInvoke);
                // 加入已连接列表
                lock (remoteTCPClients) remoteTCPClients.Add(client);
                // 新连接回调
                if (socketStatusListener != null) socketStatusListener.OnConnect(client);
                // 加入监控中
                if (monitorSocketStatusTask != null) monitorSocketStatusTask.PushCheck(client);
                // As soon as the client is connected, post a receive to the connection
                bool willRaiseEvent = client.socket.ReceiveAsync(readEventArgs);
                if (!willRaiseEvent)
                {
                    ProcessReceive(readEventArgs);
                }
            }

            // Accept the next connection request
            StartAccept(e);
        }

        /// <summary>
        /// 接受数据进程
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            // check if the remote host closed the connection
            RemoteConnection client = (RemoteConnection)e.UserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                // 重置超时标记
                if (monitorSocketStatusTask != null) Interlocked.Exchange(ref client.timeoutCount, 0);
                // increment the count of the total bytes receive by the server
                // Interlocked.Add(ref totalBytesRead, e.BytesTransferred);
                // log.Info("ProcessReceive", "The server has read a total of {0} bytes", totalBytesRead);
                // echo the data received back to the client
                byte[] buffer = new byte[e.BytesTransferred];
                Array.Copy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred);
                // Console.WriteLine(Encoding.UTF8.GetString(buffer));
                client.rBuffer.Decode(buffer);
                client.TriggerSocketInvoke();

                bool willRaiseEvent = client.socket.ReceiveAsync(e);
                if (!client.socket.isClosed && !willRaiseEvent)
                {
                    ProcessReceive(e);
                }
            }
            else RemoveExistClient(client);
        }

        /// <summary>
        /// 开始接受新客户端连接
        /// IOCP模型构建
        /// </summary>
        private void StartAcceptReceiveFrom()
        {
            Interlocked.Increment(ref numConnectedSockets);
            // log.Info("StartAcceptReceiveFrom", "Client connection accepted. There are {0} clients connected to the server", numConnectedSockets);

            // Get the socket for the accepted client connection and put it into the 
            //ReadEventArg object user token
            if (readWritePool.TryPop(out SocketAsyncEventArgs readEventArgs))
            {
                maxNumberAcceptedClients.WaitOne();
                bool willRaiseEvent = serverSocket.ReceiveFromAsync(readEventArgs);
                if (!willRaiseEvent)
                {
                    ProcessReceiveFrom(readEventArgs);
                }
            }
        }

        /// <summary>
        /// 接受数据进程 udp
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceiveFrom(SocketAsyncEventArgs e)
        {
            // check if the remote host closed the connection
            RemoteConnection client = (RemoteConnection)e.UserToken;

            // 初始化
            client.Init(Socket.FillAsClient(e.RemoteEndPoint), this, socketInvoke);
            //     // 新连接回调
            if (socketStatusListener != null) socketStatusListener.OnConnect(client);

            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                // 重置超时标记
                if (monitorSocketStatusTask != null) Interlocked.Exchange(ref client.timeoutCount, 0);
                // increment the count of the total bytes receive by the server
                // Interlocked.Add(ref totalBytesRead, e.BytesTransferred);
                // log.Info("ProcessReceiveFrom", "The server has read a total of {0} bytes", totalBytesRead);
                // echo the data received back to the client
                byte[] buffer = new byte[e.BytesTransferred];
                Array.Copy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred);
                // Console.WriteLine(System.Text.Encoding.UTF8.GetString(buffer));
                client.rBuffer.Decode(buffer);
                client.TriggerSocketInvoke();
            }

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref numConnectedSockets);
            // Free the SocketAsyncEventArg so they can be reused by another client
            readWritePool.Push(e);
            maxNumberAcceptedClients.Release();

            // 继续接受
            StartAcceptReceiveFrom();
        }

        /// <summary>
        /// IO完成端口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            // determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                case SocketAsyncOperation.ReceiveFrom:
                    ProcessReceiveFrom(e);
                    break;
                default:
                    Log.Info("IO_Completed The last operation completed on the socket was not a receive or send");
                    break;
            }
        }

        /// <summary>
        /// 线程发送
        /// </summary>
        private bool ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                // done echoing data back to the client
                // RemoteUserToken token = (RemoteUserToken)e.UserToken;
                return true;
            }
            else
            {
                RemoveExistClient((RemoteConnection)e.UserToken);
            }
            return false;
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        internal bool SendAsyncEvent(RemoteConnection client, byte[] buffer, int offset, int count, byte main, byte second)
        {
            if (client == null || client.socket == null)
                return false;
            // 重置超时标记
            if (monitorSocketStatusTask != null) Interlocked.Exchange(ref client.timeoutCount, 0);
            // 数据打包
            byte[] data = null;
            if (offset == 0 && buffer.Length == count)
            {
                data = client.rBuffer.Encode(main, second, buffer);
            }
            else if (offset > 0)
            {
                byte[] buffer2 = new byte[count];
                Array.Copy(buffer, offset, buffer2, 0, count);
                data = client.rBuffer.Encode(main, second, buffer);
            }
            // 数据发送
            client.sendEventArgs.SetBuffer(data, 0, data.Length);
            if (client.socket.socketType == SocketType.Stream)
            {
                bool willRaiseEvent = client.socket.SendAsync(client.sendEventArgs);
                if (!willRaiseEvent)
                {
                    return ProcessSend(client.sendEventArgs);
                }
                else
                    return true;
            }
            else if (client.socket.socketType == SocketType.Dgram)
            {
                bool willRaiseEvent = serverSocket.SendToAsync(client.sendEventArgs);
                if (!willRaiseEvent)
                {
                    return ProcessSend(client.sendEventArgs);
                }
                else
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 关闭客户端
        /// </summary>
        /// <param name="e"></param>
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            RemoteConnection client = e.UserToken as RemoteConnection;
            // close the socket associated with the client
            if (client.socket.socketType == SocketType.Stream)
            {
                client.isAlive = false;
                try
                {
                    client.socket.Close();
                }
                // throws if client process has already closed
                catch (Exception ex)
                {
                    Log.Exception(ex, "ServerConnection", "CloseClientSocket", "Socket");
                }
                // 断开连接回调
                if (socketStatusListener != null) socketStatusListener.OnClose(client);
                client.Target = null;
                // decrement the counter keeping track of the total number of clients connected to the server
                Interlocked.Decrement(ref numConnectedSockets);
                // Free the SocketAsyncEventArg so they can be reused by another client
                readWritePool.Push(e);
                maxNumberAcceptedClients.Release();
                // Log.Info("CloseClientSocket A client has been disconnected from the server. There are " + numConnectedSockets + " clients connected to the server" );
            }
        }

        /// <summary>
        /// 关闭服务
        /// </summary>
        public void CloseServer()
        {
            foreach (var item in remoteTCPClients.ToArray()) item.Destroy();
            remoteTCPClients.Clear();
            if (!serverSocket.isClosed) serverSocket.Close();
            if (monitorSocketStatusTask != null) monitorSocketStatusTask.Close();
        }
    }
}
