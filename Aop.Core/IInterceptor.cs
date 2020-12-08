using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Aop.Core
{
    public interface IInterceptor
    {
        /// <summary>
        /// 方法执行前
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        void BeforeEvent(MethodInfo method, object[] parameters);
        /// <summary>
        /// 方法执行后
        /// </summary>
        /// <param name="method"></param>
        /// <param name="result"></param>
        void AfterEvent(MethodInfo method, object result);
        /// <summary>
        /// 方法异常时
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        object ExceptionEvent(Exception exception, MethodInfo method);
    }
}
