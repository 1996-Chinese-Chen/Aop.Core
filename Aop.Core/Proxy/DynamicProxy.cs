using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Aop.Core
{
    public class DynamicProxy : DispatchProxy
    {
        /// <summary>
        /// 执行方法接口
        /// </summary>
        private IInterceptor interceptor { get; set; }
        /// <summary>
        /// 具体类型
        /// </summary>
        private  object service { get; set; }
        /// <summary>
        /// 创建代理
        /// </summary>
        /// <param name="targetType"></param>
        /// <param name="interceptor"></param>
        /// <param name="serviceParameter"></param>
        /// <returns></returns>
        public static object Create(Type targetType, IInterceptor interceptor, object[] serviceParameter = null)
        {
            object proxy = GetProxy(targetType);
            ((DynamicProxy)proxy).CreateInstance(interceptor);
            ((DynamicProxy)proxy).service = CreateServiceInstance(targetType, serviceParameter);
            return proxy;
        }
        /// <summary>
        /// 创建代理，targetType为类，interceptorType继承IInterceptor，serviceParameter为targetType为类构造函数的参数，parameters为interceptorType构造函数参数
        /// </summary>
        /// <param name="targetType"></param>
        /// <param name="interceptorType"></param>
        /// <param name="serviceParameter"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object Create(Type targetType, Type interceptorType, object[] serviceParameter = null, params object[] parameters)
        {
            object proxy = GetProxy(targetType);
            ((DynamicProxy)proxy).CreateInstance(interceptorType, parameters);
            ((DynamicProxy)proxy).service = CreateServiceInstance(targetType, serviceParameter);
            return proxy;
        }
        /// <summary>
        /// tIService为接口，tService实现tIService接口，intercer继承IInterceptor，serviceParameter为targetType为类构造函数的参数，parameters为interceptorType构造函数参数
        /// </summary>
        /// <param name="tIService"></param>
        /// <param name="tService"></param>
        /// <param name="intercer"></param>
        /// <param name="serviceParameter"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object Create(Type tIService,Type tService, Type intercer, object[] serviceParameter = null, params object[] parameters)
        {
            var proxy = GetProxy(tIService);
            ((DynamicProxy)proxy).CreateInstance(intercer, parameters);
            ((DynamicProxy)proxy).service = CreateServiceInstance(tService, serviceParameter);
            return proxy;
        }
        /// <summary>
        /// TTarget为接口，tService实现tIService接口，TInterceptor继承IInterceptor，serviceParameter为targetType为类构造函数的参数，parameters为interceptorType构造函数参数
        /// </summary>
        /// <typeparam name="TTarget"></typeparam>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TInterceptor"></typeparam>
        /// <param name="serviceParameter"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static TTarget Create<TTarget,TService,TInterceptor>(object[] serviceParameter=null,params object[] parameters) where TInterceptor : IInterceptor where TService:TTarget
        {
            var proxy = GetProxy(typeof(TTarget));
            ((DynamicProxy)proxy).CreateInstance(typeof(TInterceptor), parameters);
            ((DynamicProxy)proxy).service = CreateServiceInstance(typeof(TService), serviceParameter);
            return (TTarget)proxy;
        }
        /// <summary>
        /// 创建指定类型对象，servicePara构造函数参数
        /// </summary>
        /// <param name="type"></param>
        /// <param name="servicePara"></param>
        /// <returns></returns>
        private static object CreateServiceInstance(Type type, params object[] servicePara)
        {
          return  Activator.CreateInstance(type, servicePara);
        }
        /// <summary>
        /// 创建代理，表达式执行泛型方法性能优于MakeGenericMethod
        /// </summary>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private static object GetProxy(Type targetType)
        {
            var callexp = Expression.Call(typeof(DispatchProxy), nameof(DispatchProxy.Create), new[] { targetType, typeof(DynamicProxy) });
            return Expression.Lambda<Func<object>>(callexp).Compile().Invoke();
        }
        /// <summary>
        /// 创建Aop具体实现类，表达式性能优于反射性能
        /// </summary>
        /// <param name="interceptorType"></param>
        /// <param name="parameters"></param>
        private void CreateInstance(Type interceptorType, object[] parameters)
        {
            var ctorParams = parameters.Select(x => x.GetType()).ToArray();
            var paramsExp = parameters.Select(x => Expression.Constant(x));
            var newExp = Expression.New(interceptorType.GetConstructor(ctorParams), paramsExp);
            this.interceptor = Expression.Lambda<Func<IInterceptor>>(newExp).Compile()();
        }
        /// <summary>
        /// 赋值
        /// </summary>
        /// <param name="interceptor"></param>
        private void CreateInstance(IInterceptor interceptor)
        {
            this.interceptor = interceptor;
        }
        /// <summary>
        /// 实现Invole方法
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        protected override object Invoke(MethodInfo method, object[] parameters)
        {
            if (method == null) throw new Exception("无效的方法");
            try
            {
                if (this.interceptor != null)
                {
                    this.interceptor.BeforeEvent(method, parameters);
                }
                object result = method.Invoke(service, parameters);
                if (method.ReturnType.BaseType == typeof(Task))
                {
                    var resultTask = result as Task;
                    if (resultTask != null)
                    {
                        resultTask.ContinueWith(task => 
                        {
                            if (task.Exception != null)
                            {
                                if (interceptor != null)
                                {
                                   this.interceptor.ExceptionEvent(task.Exception.InnerException ?? task.Exception, method);
                                }
                            }
                            else
                            {
                                object taskResult = null;
                                if (task.GetType().GetTypeInfo().IsGenericType &&
                                      task.GetType().GetGenericTypeDefinition() == typeof(Task<>))
                                {
                                    var property = task.GetType().GetTypeInfo().GetProperties().FirstOrDefault(p => p.Name == "Result");
                                    if (property != null)
                                    {
                                        taskResult = property.GetValue(task);
                                    }
                                }
                                if (interceptor != null)
                                {
                                    this.interceptor.AfterEvent(method, taskResult);
                                }
                            }
                        });
                    }
                }
                else
                {
                    try
                    {
                        if (interceptor != null)
                        {
                            this.interceptor.AfterEvent(method, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (interceptor != null)
                        {
                            return this.interceptor.ExceptionEvent(ex, method);
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                if (interceptor != null)
                {
                    return this.interceptor.ExceptionEvent(ex, method);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
