using System.Reflection;
using Autofac;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._Log;
using Utilities.Constants;


namespace iptv.Api.Utilities.Configurations
{
    public static class ControllerAutofacConfigurationExtensions
    {
        public static void AddControllerServices(this ContainerBuilder containerBuilder)
        {
            var assembliesToRegister = new Assembly[]
            {
                typeof(ILogRepository).Assembly,
                typeof(ILogService).Assembly,
            };

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
                .AssignableTo<RegisterMode.IScopedDependency>()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
                .AssignableTo<RegisterMode.ITransientDependency>()
                .AsImplementedInterfaces()
                .InstancePerDependency();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
                .AssignableTo<RegisterMode.ISingletonDependency>()
                .AsImplementedInterfaces()
                .SingleInstance();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
               .AssignableTo<RegisterMode.ISelfSingletonDependency>()
               .AsSelf()
               .SingleInstance();

            containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
              .AssignableTo<RegisterMode.IHostedDependency>()
              .As<IHostedService>()
              .SingleInstance();
        }

    }
}
