﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NServiceBus.Host.Internal;
using Topshelf;
using Topshelf.Configuration;

namespace NServiceBus.Host
{
    public class Program
    {

        static void Main(string[] args)
        {
            
            var endpoint = GetEndpointType();
            
            var endpointName = GetEndpointName(endpoint);
            
            var endpointId = string.Format("{0} Service - v{1}", endpointName, endpoint.Assembly.GetName().Version);
            
            var endpointConfigurationFile = endpoint.Assembly.ManifestModule.Name + ".config";

            if (!File.Exists(endpointConfigurationFile))
                throw new InvalidOperationException("No configuration file found at: " + endpointConfigurationFile);

            var cfg = RunnerConfigurator.New(x =>
             {
                 x.SetDisplayName(endpointId);
                 x.SetServiceName(endpointId);
                 x.SetDescription("NServiceBus Message Endpoint Host Service");

                 x.ConfigureServiceInIsolation<GenericHost>(endpoint.AssemblyQualifiedName, c =>
                 {
                     c.ConfigurationFile(endpointConfigurationFile);
                     c.WhenStarted(service => service.Start());
                     c.WhenStopped(service => service.Stop());
                     c.CreateServiceLocator(() => new HostServiceLocator());
                 });
                 x.DoNotStartAutomatically();

                 x.RunAsFromInteractive();
             });

            Runner.Host(cfg, args);
        }

        private static Type GetEndpointType()
        {
            var endpoints = ScanAssembliesForEndpoints();

            ValidateEndpoints(endpoints);

            return endpoints.First();
        }

        public static IEnumerable<Type> ScanAssembliesForEndpoints()
        {
            foreach (var assemblyFile in Directory.GetFiles(".", "*.dll"))
            {
                var assembly = Assembly.LoadFile(Path.GetFullPath(assemblyFile));

                foreach (var type in assembly.GetTypes().Where(t => typeof(IConfigureThisEndpoint).IsAssignableFrom(t) && t != typeof(IConfigureThisEndpoint)))
                    yield return type;

            }
        }

        private static void ValidateEndpoints(IEnumerable<Type> endpoints)
        {
            if (endpoints.Count() == 0)
                throw new InvalidOperationException("No endpoints found in scanned assemlies");

            if (endpoints.Count() > 1)
                throw new InvalidOperationException("Host doesn't support hosting of multiple endpoints");

        }

        private static string GetEndpointName(Type endpoint)
        {
            if (!endpoint.IsDefined(typeof(EndpointNameAttribute), false))
            {
                return endpoint.Name;
            }

            var endpointNameAttribute = endpoint.GetCustomAttributes(typeof(EndpointNameAttribute), false)[0] as EndpointNameAttribute;

            if (endpointNameAttribute == null || string.IsNullOrEmpty(endpointNameAttribute.Name))
            {
                return endpoint.Name;
            }

            return endpointNameAttribute.Name;
        }
    }
}