using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aop.Core
{
   public class AopCore:Attribute
    {
        public AopCore(Type type, ServiceLifetime service)
        {
            Type = type;
            Service = service;
        }

        private Type Type { get; set; }
        public ServiceLifetime Service { get; }

        public Type GetAopInstance()
        {
            var bIsTrue = Type.GetInterfaces().Where(s => s == typeof(IInterceptor)).ToList().Count > 0;
            if (bIsTrue)
            {
                return Type;
            }
            return null;
        }
    }
}
