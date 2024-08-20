// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Test.Utility
{
    public class PlatformFactAttribute
        : FactAttribute
    {
        private string _skip;

        public override string Skip
        {
            get
            {
                var skip = _skip;

                if (string.IsNullOrEmpty(skip))
                {
                    if (CIOnly && !XunitAttributeUtility.IsCI)
                    {
                        skip = "This test only runs on the CI. To run it locally set the env var CI=true";
                    }
                }

                if (string.IsNullOrEmpty(skip))
                {
                    skip = XunitAttributeUtility.GetPlatformSkipMessageOrNull(GetAllPlatforms());
                }

                if (string.IsNullOrEmpty(skip))
                {
                    skip = XunitAttributeUtility.GetMonoMessage(OnlyOnMono, SkipMono, PlatformSkipMessage);
                }

                // If this is null the test will run.
                return skip;
            }

            set
            {
                throw new InvalidOperationException("Setting Skip on a platform theory disables it for all platforms. This may be unintuitive so we disallow it.")
            }
        }

        public IEnumerable<string> Platforms { get; set; } = new List<string>();

        public string Platform { get; set; }

        public IEnumerable<string> SkipPlatforms { get; set; } = new List<string>();

        public string SkipPlatform { get; set; }

        public bool OnlyOnMono { get; set; }

        public bool SkipMono { get; set; }

        public string PlatformSkipMessage { get; set; } // If this is not null, but Skip is null, then this won't work.

        public bool CIOnly { get; set; }

        /// <summary>
        /// Provide property values to use this attribute.
        /// </summary>
        public PlatformFactAttribute()
        {
        }

        /// <summary>
        /// Run only on the given platforms
        /// </summary>
        public PlatformFactAttribute(params string[] platforms)
        {
            Platforms = platforms.ToList();
        }

        private string[] GetAllPlatforms()
        {
            var platforms = new HashSet<string>(Platforms ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            {
                Platform
            };

            var skipPlatforms = new HashSet<string>(SkipPlatforms ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            {
                SkipPlatform
            };

            platforms.RemoveWhere(e => string.IsNullOrEmpty(e) || skipPlatforms.Contains(e));

            return platforms.ToArray();
        }
    }
}
