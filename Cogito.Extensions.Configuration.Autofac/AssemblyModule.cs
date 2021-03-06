﻿using System;
using System.Collections.Generic;
using System.Linq;

using Autofac;
using Autofac.Core;
using Autofac.Core.Activators.Delegate;
using Autofac.Core.Lifetime;
using Autofac.Core.Registration;

using Cogito.Autofac;
using Cogito.Linq;

using Microsoft.Extensions.Configuration;

namespace Cogito.Extensions.Configuration.Autofac
{

    public class AssemblyModule : ModuleBase
    {

        static readonly Guid ROOT_ID = Guid.NewGuid();
        static readonly Guid CONF_ID = Guid.NewGuid();

        protected override void Register(ContainerBuilder builder)
        {
            builder.RegisterModule<Cogito.Autofac.AssemblyModule>();

            builder.RegisterFromAttributes(typeof(AssemblyModule).Assembly);

            // this source provides the new IConfigurationRoot service by chaining to any existing IConfiguration instances registered within the container.
            builder.RegisterSource(new ConfigurationRegistrationSource());

            // this provides a new IConfiguration service that uses the new IConfigurationRoot service
            builder.RegisterComponent(new ComponentRegistration(
                CONF_ID,
                new DelegateActivator(typeof(IConfiguration), (c, p) => c.Resolve<IConfigurationRoot>()),
                new RootScopeLifetime(),
                InstanceSharing.Shared,
                InstanceOwnership.ExternallyOwned,
                new TypedService(typeof(IConfiguration)).Yield(),
                new Dictionary<string, object>()));
        }

        /// <summary>
        /// Generates <see cref="IConfigurationRoot"/> registrations depending on existing environment.
        /// </summary>
        class ConfigurationRegistrationSource : IRegistrationSource
        {

            public bool IsAdapterForIndividualComponents => false;

            public IEnumerable<IComponentRegistration> RegistrationsFor(Service service, Func<Service, IEnumerable<ServiceRegistration>> registrationAccessor)
            {
                if (service is IServiceWithType svc && svc.ServiceType == typeof(IConfigurationRoot))
                    yield return new ComponentRegistration(
                        ROOT_ID,
                        GetActivator(registrationAccessor(new TypedService(typeof(IConfiguration))).Where(i => i.Registration.Id != CONF_ID)),
                        new RootScopeLifetime(),
                        InstanceSharing.Shared,
                        InstanceOwnership.OwnedByLifetimeScope,
                        service.Yield(),
                        new Dictionary<string, object>());
            }

            /// <summary>
            /// Gets the activator based on whether existing registrations are present.
            /// </summary>
            /// <param name="existing"></param>
            /// <returns></returns>
            DelegateActivator GetActivator(IEnumerable<ServiceRegistration> existing)
            {
                return new DelegateActivator(typeof(IConfigurationRoot), (c, p) => c.Resolve<IConfigurationRootBuilder>(TypedParameter.From(existing.Select(i => c.ResolveComponent(new ResolveRequest(new TypedService(typeof(IConfiguration)), i, Enumerable.Empty<Parameter>()))).OfType<IConfiguration>())).BuildConfiguration());
            }

        }

    }

}
