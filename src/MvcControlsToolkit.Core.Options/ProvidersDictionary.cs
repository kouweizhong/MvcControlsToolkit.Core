﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MvcControlsToolkit.Core.Options
{
    internal class ProvidersDictionary
    {
        private static IDictionary<string, List<IOptionsProvider>> allProviders = new ConcurrentDictionary<string, List<IOptionsProvider>>();
        private HashSet<IOptionsProvider> requestProviders = new HashSet<IOptionsProvider>();
        public static void Add(IOptionsProvider provider)
        {
            string prefix = provider.Prefix;
            List<IOptionsProvider> entry = null;
            if (allProviders.TryGetValue(prefix, out entry)){
                entry.Add(provider);
            }
            else {
                var l = allProviders[prefix] = new List<IOptionsProvider>();
                l.Add(provider);
            }
        }
        public void AddToRequest(string prefix, HttpContext context, IOptionsDictionary dict)
        {
            uint maxPriority = 0;
            List<IOptionsProvider> addedProviders = new List<IOptionsProvider>();
            HashSet<IOptionsProvider> set = new HashSet<IOptionsProvider>();
            foreach (var x in allProviders)
            {
                if (x.Key == prefix || (prefix.StartsWith(x.Key) && prefix[x.Key.Length] == '.')){
                    foreach (var y in x.Value)
                    {
                        
                        if (y.Enabled(context) && !requestProviders.Contains(y))
                        {
                            if (y.Priority > maxPriority) maxPriority = y.Priority;
                            addedProviders.Add(y);
                            set.UnionWith(y.Load(context, dict));
                            requestProviders.Add(y);
                        }
                    }
                }
            }
            foreach(var x in addedProviders)
            {
                if (x.AutoCreate && x.CanSave && x.Priority < maxPriority) set.Add(x);
            }
            foreach(var x in set)
            {
                if (x.Enabled(context) && x.CanSave && (x.AutoSave || x.AutoCreate)) x.Save(context, dict);
            }
        }
    }
}
