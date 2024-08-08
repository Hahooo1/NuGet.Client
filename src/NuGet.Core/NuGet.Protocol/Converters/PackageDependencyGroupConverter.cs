// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace NuGet.Protocol
{
    public class PackageDependencyGroupConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => (objectType == typeof(PackageDependencyGroup));

        public override bool CanWrite => false;

        private readonly PackageDependencyConverter _converter = new PackageDependencyConverter();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string fxName = null;
            List<Packaging.Core.PackageDependency> packages = new List<Packaging.Core.PackageDependency>();
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType.Equals(JsonToken.PropertyName))
                {
                    if (reader.Value.Equals(JsonProperties.Dependencies))
                    {
                        // Dependencies are stored in an array
                        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                        {
                            Packaging.Core.PackageDependency package = (Packaging.Core.PackageDependency)_converter.ReadJson(reader, typeof(Packaging.Core.PackageDependency), null, serializer);
                            packages.Add(package);
                        }
                    }
                    else if (reader.Value.Equals(JsonProperties.TargetFramework))
                    {
                        reader.Read();
                        fxName = reader.Value.ToString();
                    }
                }
            }

            var framework = NuGetFramework.AnyFramework;

            if (!string.IsNullOrEmpty(fxName))
            {
                framework = NuGetFramework.Parse(fxName);
                fxName = framework.GetShortFolderName();
            }

            return new PackageDependencyGroup(framework, packages);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
