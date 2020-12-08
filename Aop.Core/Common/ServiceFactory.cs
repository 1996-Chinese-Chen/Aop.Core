using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Aop.Core
{
   public static class ServiceFactory
    {
        /// <summary>
        /// 返回服务代理,TIService为接口类型，TService为实现类，TAop为IInterceptor实现类
        /// </summary>
        /// <typeparam name="TIService"></typeparam>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TAop"></typeparam>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static TIService GetService<TIService, TService, TAop>(IServiceProvider provider) where TService:TIService where TAop:IInterceptor
        {
            var parameter = GetCtorParameter(typeof(TService), provider);
            var aopParameter = GetCtorParameter(typeof(TAop), provider);
            var user = DynamicProxy.Create<TIService, TService, TAop>(parameter, aopParameter);
            return user;
        }
        /// <summary>
        /// 返回服务代理,TService为类，TAop为IInterceptor实现类
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TAop"></typeparam>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static TService GetService<TService, TAop>(IServiceProvider provider)  where TAop : IInterceptor where TService:class,new()
        {
            var ctorparameter = GetCtorParameter(typeof(TService), provider);
            var aopParameter= GetCtorParameter(typeof(TAop), provider);
            var user = DynamicProxy.Create(typeof(TService), typeof(TAop), ctorparameter, aopParameter);
            return user as TService;
        }
        /// <summary>
        /// 返回服务代理,tService为类，TAop为IInterceptor实现类
        /// </summary>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static object GetService(Type tService,Type tAop,IServiceProvider provider)
        {
            var ctorparameter = GetCtorParameter(tService, provider);
            var aopParameter = GetCtorParameter(tAop, provider);
            var user = DynamicProxy.Create(tService, tAop, ctorparameter, aopParameter);
            return user;
        }
        /// <summary>
        ///  返回服务代理,tIService为接口类型，tService为实现类，tAop为IInterceptor实现类
        /// </summary>
        /// <param name="tIService"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static object GetService(Type tIService,Type tService, Type tAop, IServiceProvider provider)
        {
            var parameter = GetCtorParameter(tService, provider);
            var aopParameter = GetCtorParameter(tAop, provider);
            var user = DynamicProxy.Create(tIService, tService, tAop,parameter, aopParameter);
            return user;
        }
        /// <summary>
        /// 获取指定类型构造参数
        /// </summary>
        /// <param name="type"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static object[] GetCtorParameter(Type type, IServiceProvider provider)
        {
            if (type is null)
            {
                return null;
            }
            List<Object> listresult = new List<object>();
            var ctors = type.GetConstructors();
            if (ctors.Length > 0)
            {
                var parameters = ctors.FirstOrDefault().GetParameters();
                foreach (var parameter in parameters)
                {
                    var instance = provider.GetService(parameter.ParameterType);
                    if (instance != null)
                    {
                        listresult.Add(instance);
                    }
                    else
                    {
                        listresult.Add(CreateInstance(parameter.ParameterType, provider));
                    }
                }
                return listresult.ToArray();
            }
            return null;
        }
        /// <summary>
        /// 创建指定类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static object CreateInstance(Type type, IServiceProvider provider)
        {
            if (type is null)
            {
                return null;
            }
            var ctors = type.GetConstructors();
            if (ctors.Length > 0)
            {
                var parameters = ctors.FirstOrDefault().GetParameters();
                List<object> list = new List<object>();
                foreach (var parameter in parameters)
                {
                    var instanceObj = provider.GetService(parameter.ParameterType);
                    if (instanceObj != null)
                    {
                        list.Add(instanceObj);
                    }
                    else
                    {
                        list.Add(CreateInstance(parameter.ParameterType, provider));
                    }
                }
                var ctorParams = list.Select(x => x.GetType()).ToArray();
                var paramsExp = list.Select(x => Expression.Constant(x));
                var newExp = Expression.New(type.GetConstructor(ctorParams), paramsExp);
                var instance = Expression.Lambda<Func<object>>(newExp).Compile()();
                return instance;
            }
            else
            {
                var newExp = Expression.New(type);
               var instance = Expression.Lambda<Func<object>>(newExp).Compile()();
               return instance;
            }
        }

        /// <summary>
        /// 拓展方法使用AopCore
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static IServiceCollection AddAopCore(this IServiceCollection service)
        {
           Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory).Where(s=>!s.Contains("System")&&!s.Contains("Microsoft")&&s.EndsWith("dll")).ToList().ForEach(s=> {
               Assembly assembly = Assembly.LoadFrom(s);
               var listAop = assembly.GetExportedTypes().Where(t => t.GetCustomAttributes(true).Length > 0).ToList();
               if (listAop.Count > 0)
               {
                   listAop.ForEach(y =>
                   {
                       var attribute = y.GetCustomAttributes(true).Where(sa => sa.GetType() == typeof(AopCore)).ToList();
                       if (attribute.Count > 0)
                       {
                           var aopCore = attribute.FirstOrDefault() as AopCore;
                           RegisterService(service, aopCore,y);
                       }
                   });
               }
           });
            return service;
        }
        /// <summary>
        /// 注继承接口类型注入（单例）
        /// </summary>
        /// <param name="serviceDescriptors"></param>
        /// <param name="tIService"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        private static void AddSingleton(IServiceCollection serviceDescriptors,Type tIService,Type tService, Type tAop)
        {
            serviceDescriptors.AddSingleton(tIService,s=> {
                return ServiceFactory.GetService(tIService, tService, tAop, s);
            });
        }
        /// <summary>
        /// class类型注入（单例）
        /// </summary>
        /// <param name="serviceDescriptors"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        /// 
        private static void AddSingleton(IServiceCollection serviceDescriptors, Type tService, Type tAop)
        {
            serviceDescriptors.AddSingleton(tService, s => {
                return ServiceFactory.GetService(tService, tAop, s);
            });
        }
        /// <summary>
        /// 注继承接口类型注入（作用域）
        /// </summary>
        /// <param name="services"></param>
        /// <param name="tIService"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        private static void AddScoped(IServiceCollection services, Type tIService,Type tService, Type tAop)
        {
            services.AddScoped(tIService, s =>
            {
                return ServiceFactory.GetService(tIService, tService, tAop, s);
            });
        }
        /// <summary>
        /// class类型注入（作用域）
        /// </summary>
        /// <param name="services"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        private static void AddScoped(IServiceCollection services, Type tService, Type tAop)
        {
            services.AddScoped(tService, s =>
            {
                return ServiceFactory.GetService(tService, tAop, s);
            });
        }
        /// <summary>
        ///  注继承接口类型注入（瞬时）
        /// </summary>
        /// <param name="services"></param>
        /// <param name="tIService"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        private static void AddTransient(IServiceCollection services, Type tIService, Type tService, Type tAop)
        {
            services.AddTransient(tIService, s =>
            {
                return ServiceFactory.GetService(tIService, tService, tAop, s);
            });
        }
        /// <summary>
        /// class类型注入（瞬时）
        /// </summary>
        /// <param name="services"></param>
        /// <param name="tService"></param>
        /// <param name="tAop"></param>
        private static void AddTransient(IServiceCollection services, Type tService, Type tAop)
        {
            services.AddTransient(tService, s =>
            {
                return ServiceFactory.GetService(tService, tService, tAop, s);
            });
        }
        /// <summary>
        /// 实例注入
        /// </summary>
        /// <param name="services"></param>
        /// <param name="core"></param>
        /// <param name="type"></param>
        private static void RegisterService(IServiceCollection services,AopCore core,Type type)
        {
            if (type is null) throw new ArgumentNullException("参数Type不能为空");
            var hhgh = type.GetInterfaces();
            if (type.GetInterfaces().Length>0)
            {
                switch (core.Service)
                {
                    case ServiceLifetime.Singleton:
                        AddSingleton(services, type.GetInterfaces().FirstOrDefault(), type, core.GetAopInstance());
                        break;
                    case ServiceLifetime.Scoped:
                        AddScoped(services, type.GetInterfaces().FirstOrDefault(), type, core.GetAopInstance());
                        break;
                    case ServiceLifetime.Transient:
                        AddTransient(services, type.GetInterfaces().FirstOrDefault(), type, core.GetAopInstance());
                        break;
                }
            }
            else
            {
                switch (core.Service)
                {
                    case ServiceLifetime.Singleton:
                        AddSingleton(services, type, core.GetAopInstance());
                        break;
                    case ServiceLifetime.Scoped:
                        AddScoped(services, type, core.GetAopInstance());
                        break;
                    case ServiceLifetime.Transient:
                        AddTransient(services, type, core.GetAopInstance());
                        break;
                }
            }
        }
    }
}
