﻿using System;

namespace ES.Network.Http.Linq
{
    /// <summary>
    /// 捕捉接受异常接口
    /// </summary>
    public interface IHttpVisitor
    {
        /// <summary>
        /// 异常捕捉回调
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="ex"></param>
        void HttpVisitorException(HttpConnection conn, Exception ex);
    }
}
