﻿using ES.Network.Http;
using System;
using System.Collections.Generic;

namespace ES.Network.Visitor
{
    /// <summary>
    /// http访问器
    /// </summary>
    public class HttpVisitor : HttpInvoke
    {
        /// <summary>
        /// 回调委托
        /// </summary>
        public delegate void OnRequest(HttpConnection conn);
        /// <summary>
        /// 回调委托列表
        /// </summary>
        internal Dictionary<string, OnRequest> commandList = null;
        /// <summary>
        /// 全局Http监听者
        /// </summary>
        private OnRequest allHttpListener = null;
        /// <summary>
        /// 异常回调函数地址
        /// </summary>
        private readonly IHttpVisitorException catchReceivedException = null;

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpVisitor(IHttpVisitorException catchReceivedException)
        {
            commandList = new Dictionary<string, OnRequest>();
            this.catchReceivedException = catchReceivedException;
        }

        /// <summary>
        /// 添加访问函数并用函数名作为回调地址
        /// </summary>
        /// <param name="callback">访问函数</param>
        public void Add(OnRequest callback)
        {
            lock (commandList)
            {
                commandList.Add(nameof(callback), callback);
            }
        }

        /// <summary>
        /// 添加访问函数
        /// </summary>
        /// <param name="suffix">标记后缀</param>
        /// <param name="callback">访问函数</param>
        public void Add(string suffix, OnRequest callback)
        {
            lock (commandList)
            {
                commandList.Add(suffix, callback);
            }
        }

        /// <summary>
        /// 设置全局监听者
        /// 虽然访问器可以监听各个想要监听的地址
        /// 但是无法有一个共同回调来处理一些特殊的需求
        /// 这个监听是返回所有可以接收到的请求,以此来实现添加Add无法实现的全部监听
        /// </summary>
        /// <param name="callback"></param>
        public void SetAllListener(OnRequest callback)
        {
            allHttpListener = callback;
        }

        void HttpInvoke.OnRequest(HttpConnection conn)
        {
            OnRequest or = null;
            lock (commandList)
            {
                if (commandList.TryGetValue(conn.suffix, out OnRequest value))
                {
                    or = value;
                }
            }
            if (or != null)
            {
                try
                {
                    if (allHttpListener != null) allHttpListener.Invoke(conn);
                    or.Invoke(conn);
                }
                catch (Exception ex)
                {
                    if (catchReceivedException != null) catchReceivedException.CatchOnRequestException(conn, ex);
                    else throw (ex);
                    conn.response.StatusCode = (int)HttpRequestState.NonExistent;
                }
            }
            else
                conn.response.StatusCode = (int)HttpRequestState.NonExistent;
        }
    }
}
